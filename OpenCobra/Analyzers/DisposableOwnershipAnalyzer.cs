// DisposableOwnershipAnalyzer
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace OpenCobra.Analyzers;

/// <summary>
/// Flags disposable ownership bugs that <see cref="UnownedReferenceAnalyzer"/> (GDK001) doesn't
/// cover: a type storing (aliasing, not copying) a constructor argument that it will later
/// dispose, without the argument being marked <c>[TakesOwnership]</c> (GDK002); and a caller that
/// disposes an argument independently after passing it to a <c>[TakesOwnership]</c> parameter,
/// double-disposing the shared instance (GDK003).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DisposableOwnershipAnalyzer : DiagnosticAnalyzer {
  public static readonly string UndeclaredOwnershipId = "GDK002";
  public static readonly string DoubleDisposeId = "GDK003";

  private const string Category = "Design";
  private const string TakesOwnershipAttributeName = "TakesOwnershipAttribute";

  private static readonly DiagnosticDescriptor UndeclaredOwnershipRule =
      new("GDK002", "Disposable field aliases an un-annotated parameter", "{0}",
          Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
          description: "A type whose Dispose() disposes a field populated directly from a constructor " +
                        "parameter aliases the caller's IDisposable instance rather than owning a copy of " +
                        "it. Mark the parameter [TakesOwnership] to document the transfer (and audit the " +
                        "call site for a matching double-dispose), or copy the value instead of aliasing it.");

  private static readonly DiagnosticDescriptor DoubleDisposeRule =
      new("GDK003", "Disposable disposed again after ownership transfer", "{0}",
          Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
          description: "An argument passed to a [TakesOwnership] parameter is also disposed " +
                        "independently in the same scope (via 'using' or an explicit Dispose() call), " +
                        "double-disposing the shared instance.");

  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
      ImmutableArray.Create(UndeclaredOwnershipRule, DoubleDisposeRule);

  public override void Initialize(AnalysisContext context) {
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();

    context.RegisterSyntaxNodeAction(AnalyzePrimaryConstructorParameter, SyntaxKind.Parameter);
    context.RegisterSyntaxNodeAction(AnalyzeConstructorBody, SyntaxKind.ConstructorDeclaration);
    context.RegisterSyntaxNodeAction(AnalyzeArgument, SyntaxKind.Argument);
  }

  // GDK002, shape 1: `class Texture(Image<Rgba32> texture) : IDisposable { Frames { get; init; } = [texture]; }`
  // — a primary-constructor parameter aliased by a property's default initializer.
  private void AnalyzePrimaryConstructorParameter(SyntaxNodeAnalysisContext context) {
    var parameter = (ParameterSyntax)context.Node;
    if (parameter.Parent?.Parent is not TypeDeclarationSyntax typeDecl)
      return; // only primary-constructor parameters live directly under the type declaration

    var semanticModel = context.SemanticModel;
    if (semanticModel.GetDeclaredSymbol(parameter) is not IParameterSymbol parameterSymbol)
      return;

    if (!ImplementsIDisposable(parameterSymbol.Type) || HasTakesOwnershipAttribute(parameterSymbol))
      return;

    if (semanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol typeSymbol ||
        !ImplementsIDisposable(typeSymbol))
      return;

    var disposeMethod = FindParameterlessDispose(typeDecl);
    if (disposeMethod == null)
      return;

    var aliasingProperty = typeDecl.Members
        .OfType<PropertyDeclarationSyntax>()
        .FirstOrDefault(p =>
            p.Initializer != null &&
            ReferencesIdentifier(p.Initializer.Value, parameter.Identifier.Text) &&
            ReferencesIdentifier(disposeMethod, p.Identifier.Text));

    if (aliasingProperty == null)
      return;

    context.ReportDiagnostic(Diagnostic.Create(
        UndeclaredOwnershipRule,
        aliasingProperty.Initializer!.GetLocation(),
        $"'{typeSymbol.Name}.{aliasingProperty.Identifier.Text}' aliases un-annotated parameter " +
        $"'{parameter.Identifier.Text}', and '{typeSymbol.Name}.Dispose()' disposes it. " +
        $"Mark the parameter [TakesOwnership] or copy the value instead of aliasing it."));
  }

  // GDK002, shape 2: `Texture(Image<Rgba32> texture) { Frames = [texture]; }` (ordinary constructor,
  // body assignment) — same aliasing bug, different syntax.
  private void AnalyzeConstructorBody(SyntaxNodeAnalysisContext context) {
    var ctorDecl = (ConstructorDeclarationSyntax)context.Node;
    if (ctorDecl.Parent is not TypeDeclarationSyntax typeDecl)
      return;

    var semanticModel = context.SemanticModel;
    if (semanticModel.GetDeclaredSymbol(ctorDecl) is not IMethodSymbol ctorSymbol ||
        semanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol typeSymbol ||
        !ImplementsIDisposable(typeSymbol))
      return;

    var disposeMethod = FindParameterlessDispose(typeDecl);
    if (disposeMethod == null)
      return;

    var assignments = ctorDecl.Body?.DescendantNodes().OfType<AssignmentExpressionSyntax>()
        ?? Enumerable.Empty<AssignmentExpressionSyntax>();

    foreach (var assignment in assignments) {
      if (assignment.Right is not IdentifierNameSyntax rightId)
        continue;

      if (semanticModel.GetSymbolInfo(rightId).Symbol is not IParameterSymbol parameterSymbol ||
          !SymbolEqualityComparer.Default.Equals(parameterSymbol.ContainingSymbol, ctorSymbol))
        continue;

      if (!ImplementsIDisposable(parameterSymbol.Type) || HasTakesOwnershipAttribute(parameterSymbol))
        continue;

      var memberName = assignment.Left switch {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access => access.Name.Identifier.Text,
        _ => null
      };

      if (memberName == null || !ReferencesIdentifier(disposeMethod, memberName))
        continue;

      context.ReportDiagnostic(Diagnostic.Create(
          UndeclaredOwnershipRule,
          assignment.GetLocation(),
          $"'{typeSymbol.Name}.{memberName}' is assigned directly from un-annotated parameter " +
          $"'{parameterSymbol.Name}', and '{typeSymbol.Name}.Dispose()' disposes it. " +
          $"Mark the parameter [TakesOwnership] or copy the value instead of aliasing it."));
    }
  }

  // GDK003: an argument passed to a [TakesOwnership] parameter, where the same local is also
  // disposed independently (via `using` or an explicit .Dispose() call) in the same block.
  private void AnalyzeArgument(SyntaxNodeAnalysisContext context) {
    var argument = (ArgumentSyntax)context.Node;
    if (argument.Expression is not IdentifierNameSyntax identifier)
      return;

    var semanticModel = context.SemanticModel;
    var parameterSymbol = ResolveParameter(argument, semanticModel);
    if (parameterSymbol == null || !HasTakesOwnershipAttribute(parameterSymbol))
      return;

    var identifierName = identifier.Identifier.Text;
    var enclosingBlock = argument.FirstAncestorOrSelf<BlockSyntax>();
    if (enclosingBlock == null)
      return;

    var usingDecl = enclosingBlock.DescendantNodes()
        .OfType<LocalDeclarationStatementSyntax>()
        .FirstOrDefault(d =>
            d.UsingKeyword.IsKind(SyntaxKind.UsingKeyword) &&
            d.Declaration.Variables.Any(v => v.Identifier.Text == identifierName));

    if (usingDecl != null) {
      context.ReportDiagnostic(Diagnostic.Create(
          DoubleDisposeRule,
          usingDecl.GetLocation(),
          $"'{identifierName}' is declared with 'using' but also passed here to parameter " +
          $"'{parameterSymbol.Name}', which takes ownership; this 'using' will double-dispose it. " +
          $"Drop 'using' on '{identifierName}', or remove [TakesOwnership] if it shouldn't transfer here."));
      return;
    }

    var explicitDispose = enclosingBlock.DescendantNodes()
        .OfType<InvocationExpressionSyntax>()
        .FirstOrDefault(inv =>
            inv.Expression is MemberAccessExpressionSyntax
              { Name.Identifier.Text: "Dispose", Expression: IdentifierNameSyntax id } &&
            id.Identifier.Text == identifierName);

    if (explicitDispose != null) {
      context.ReportDiagnostic(Diagnostic.Create(
          DoubleDisposeRule,
          explicitDispose.GetLocation(),
          $"'{identifierName}' is disposed here after being passed to [TakesOwnership] parameter " +
          $"'{parameterSymbol.Name}'; this double-disposes the shared instance."));
    }
  }

  private static IParameterSymbol? ResolveParameter(ArgumentSyntax argument, SemanticModel semanticModel) {
    if (argument.Parent is not ArgumentListSyntax argList)
      return null;

    ISymbol? invokedSymbol = argList.Parent switch {
      InvocationExpressionSyntax invocation => semanticModel.GetSymbolInfo(invocation).Symbol,
      ObjectCreationExpressionSyntax creation => semanticModel.GetSymbolInfo(creation).Symbol,
      ImplicitObjectCreationExpressionSyntax implicitCreation => semanticModel.GetSymbolInfo(implicitCreation).Symbol,
      _ => null
    };

    if (invokedSymbol is not IMethodSymbol method)
      return null;

    if (argument.NameColon != null)
      return method.Parameters.FirstOrDefault(p => p.Name == argument.NameColon.Name.Identifier.Text);

    var index = argList.Arguments.IndexOf(argument);
    return index >= 0 && index < method.Parameters.Length ? method.Parameters[index] : null;
  }

  private static MethodDeclarationSyntax? FindParameterlessDispose(TypeDeclarationSyntax typeDecl) =>
      typeDecl.Members.OfType<MethodDeclarationSyntax>()
          .FirstOrDefault(m => m.Identifier.Text == "Dispose" && m.ParameterList.Parameters.Count == 0);

  private static bool ImplementsIDisposable(ITypeSymbol type) =>
      type.Name == "IDisposable" || type.AllInterfaces.Any(i => i.Name == "IDisposable");

  private static bool HasTakesOwnershipAttribute(IParameterSymbol symbol) =>
      symbol.GetAttributes().Any(a => a.AttributeClass?.Name == TakesOwnershipAttributeName);

  private static bool ReferencesIdentifier(SyntaxNode? node, string identifierText) =>
      node != null && node.DescendantNodesAndSelf()
          .OfType<IdentifierNameSyntax>()
          .Any(id => id.Identifier.Text == identifierText);
}
