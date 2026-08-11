// ReentrancyAnalyzer
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace OpenCobra.Analyzers;

/// <summary>
/// Flags potential reentrancy bugs where blocking or long-running operations are called
/// from within a System's Update method, particularly during the Render phase.
/// Systems scheduled during Render phase should not block or perform I/O operations,
/// as this can block the UI thread and prevent responsive event handling.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ReentrancyAnalyzer : DiagnosticAnalyzer {
  public static readonly string ReentrancyWarningId = "GDK004";

  private const string Category = "Performance";

  private static readonly DiagnosticDescriptor BlockingOperationInRenderPhaseRule =
      new(ReentrancyWarningId, "Blocking operation in render-phase code", "{0}",
          Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
          description: "Systems scheduled during PipelinePhase.Render should not perform blocking " +
                        "I/O operations (Load, Wait, Sleep) or call synchronous methods that might block, " +
                        "as this blocks the UI thread and prevents responsive event handling. " +
                        "Move blocking operations to PipelinePhase.Early or PipelinePhase.Update instead.");

  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
      ImmutableArray.Create(BlockingOperationInRenderPhaseRule);

  public override void Initialize(AnalysisContext context) {
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();

    context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
  }

  private void AnalyzeInvocation(SyntaxNodeAnalysisContext context) {
    var invocation = (InvocationExpressionSyntax)context.Node;
    var semanticModel = context.SemanticModel;

    // Get the symbol being invoked
    if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodSymbol)
      return;

    // Check if this is a blocking operation (Load, Wait, Sleep, etc.)
    if (!IsBlockingOperation(methodSymbol))
      return;

    // Walk up the syntax tree to find if we're inside a System.Update method
    if (!IsInsideRenderPhaseUpdate(invocation, semanticModel))
      return;

    var diagnostic = Diagnostic.Create(
        BlockingOperationInRenderPhaseRule,
        invocation.GetLocation(),
        $"Calling '{methodSymbol.ContainingType?.Name}.{methodSymbol.Name}' in render-phase code " +
        "blocks the UI thread. Move this operation to PipelinePhase.Early or PipelinePhase.Update.");

    context.ReportDiagnostic(diagnostic);
  }

  private static bool IsBlockingOperation(IMethodSymbol method) {
    var methodName = method.Name;
    var typeName = method.ContainingType?.Name ?? "";

    // Common blocking operation names
    var blockingMethodNames = new[] {
      "Load",
      "LoadAsync",
      "Wait",
      "WaitOne",
      "WaitAll",
      "WaitAny",
      "Sleep",
      "Join",
      "ReadLine",
      "ReadToEnd",
      "Read",
      "ReadExactly",
      "SaveAsync",
      "Save",
      "Result",
      "GetResult",
    };

    if (blockingMethodNames.Contains(methodName))
      return true;

    // Type-specific blocking operations
    var typeSpecificBlockingOps = new[] {
      ("Task", "Wait"),
      ("Task", "GetResult"),
      ("Task`1", "Result"),
      ("Progress", "MeasureTasks"),
    };

    foreach (var (type, method_name) in typeSpecificBlockingOps) {
      if (typeName == type && methodName == method_name)
        return true;
    }

    return false;
  }

  private static bool IsInsideRenderPhaseUpdate(SyntaxNode node, SemanticModel semanticModel) {
    var current = node.Parent;

    while (current != null) {
      // Check if we're inside a method declaration
      if (current is MethodDeclarationSyntax method) {
        // Check if this method is an override of System.Update
        if (IsUpdateMethod(method, semanticModel)) {
          // Check if the containing class is a System with Render phase
          if (IsRenderPhaseSystem(method, semanticModel))
            return true;
        }
      }

      current = current.Parent;
    }

    return false;
  }

  private static bool IsUpdateMethod(MethodDeclarationSyntax method, SemanticModel semanticModel) {
    if (method.Identifier.Text != "Update")
      return false;

    // Check if parameters match System.Update(TimeSpan delta)
    if (method.ParameterList.Parameters.Count != 1)
      return false;

    var param = method.ParameterList.Parameters[0];
    var paramType = semanticModel.GetTypeInfo(param.Type!).Type;
    return paramType?.Name == "TimeSpan";
  }

  private static bool IsRenderPhaseSystem(MethodDeclarationSyntax method, SemanticModel semanticModel) {
    // Walk up to find the class/record declaration
    if (method.Parent is not TypeDeclarationSyntax classDecl)
      return false;

    // Get the class symbol
    if (semanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
      return false;

    // Check if it inherits from System
    var baseType = classSymbol.BaseType;
    while (baseType != null) {
      if (baseType.Name == "System" && baseType.ContainingNamespace?.ToDisplayString() == "OpenCobra.GDK.Game")
        break;
      baseType = baseType.BaseType;
    }

    if (baseType == null)
      return false;

    // Check the constructor to see if it uses PipelinePhase.Render
    var constructor = classDecl.Members
        .OfType<ConstructorDeclarationSyntax>()
        .FirstOrDefault();

    if (constructor?.Initializer?.Kind() == SyntaxKind.BaseConstructorInitializer) {
      var initializer = constructor.Initializer;
      var args = initializer.ArgumentList?.Arguments;
      if (args?.Count > 0) {
        var firstArg = args.Value[0];
        return IsRenderPipelinePhase(firstArg.Expression, semanticModel);
      }
    }

    return false;
  }

  private static bool IsRenderPipelinePhase(ExpressionSyntax expression, SemanticModel semanticModel) {
    // Resolve the actual symbol/value of the expression
    var symbolInfo = semanticModel.GetSymbolInfo(expression);

    // Handle PipelinePhase.Render member access
    if (symbolInfo.Symbol is IFieldSymbol fieldSymbol) {
      if (fieldSymbol.Name == "Render" &&
          fieldSymbol.ContainingType?.Name == "PipelinePhase" &&
          fieldSymbol.ContainingType.ContainingNamespace?.ToDisplayString() == "OpenCobra.GDK.Game")
        return true;
    }

    // Handle explicit enum member (e.g., via variable/constant)
    if (symbolInfo.Symbol is IPropertySymbol propertySymbol) {
      if (propertySymbol.Name == "Render" &&
          propertySymbol.ContainingType?.Name == "PipelinePhase" &&
          propertySymbol.ContainingType.ContainingNamespace?.ToDisplayString() == "OpenCobra.GDK.Game")
        return true;
    }

    return false;
  }
}
