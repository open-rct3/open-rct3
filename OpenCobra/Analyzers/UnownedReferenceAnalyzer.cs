using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace OpenCobra.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UnownedReferenceAnalyzer : DiagnosticAnalyzer {
  public static readonly string UnownedRefId = "GDK001";

  private const string Category = "Design";

  // Plain strings implicitly convert to LocalizableString. Do not switch these back to
  // LocalizableResourceString + ResourceManager without also adding a real embedded .resx —
  // the previous version threw MissingManifestResourceException (AD0001) on every build of
  // every consuming project, because no .resources file was ever embedded in this assembly.
  private static readonly DiagnosticDescriptor Rule =
      new("GDK001", "Unowned reference accessed unsafely", "{0}",
          Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
          description: "Fields marked with [Unowned] should use WeakReference<T> and " +
                        "should always check if the target is alive before accessing it.");

  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

  public override void Initialize(AnalysisContext context) {
    context.ConfigureGeneratedCodeAnalysis(
        GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();

    // Analyze member access expressions (field/property usage)
    context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
  }

  private void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context) {
    var memberAccess = (MemberAccessExpressionSyntax)context.Node;
    var semanticModel = context.SemanticModel;

    // Get the symbol being accessed
    if (semanticModel.GetSymbolInfo(memberAccess.Name).Symbol is not IFieldSymbol symbol)
      return;

    // Check if this field has the [Unowned] attribute
    if (!HasUnownedAttribute(symbol))
      return;

    // Check if it's a WeakReference<T>
    if (!IsWeakReferenceType(symbol.Type))
      return;

    // Check if we're accessing .Target or .IsAlive without null check
    var parent = memberAccess.Parent;

    if (parent is MemberAccessExpressionSyntax parentAccess) {
      var memberName = parentAccess.Name.Identifier.Text;
      if (memberName == "Target" || memberName == "IsAlive") {
        // Check if this access is protected by a null check
        if (!IsProtectedByNullCheck(context, memberAccess)) {
          var diagnostic = Diagnostic.Create(
              Rule,
              memberAccess.GetLocation(),
              $"Unowned reference '{symbol.Name}' accessed without null check");

          context.ReportDiagnostic(diagnostic);
        }
      }
    }
  }

  private bool HasUnownedAttribute(IFieldSymbol symbol) => symbol.GetAttributes()
    .Any(attr => attr.AttributeClass?.Name == "UnownedAttribute");

  private static bool IsWeakReferenceType(ITypeSymbol type) {
    if (type is INamedTypeSymbol namedType)
      return namedType.Name == "WeakReference" ||
             (namedType.IsGenericType &&
              namedType.ConstructedFrom.Name == "WeakReference");

    return false;
  }

  private bool IsProtectedByNullCheck(SyntaxNodeAnalysisContext context, SyntaxNode memberAccess) {
    // Walk up the syntax tree to find if we're inside an if statement
    // checking for null or calling TryGetTarget
    var current = memberAccess.Parent;

    while (current != null) {
      // Check for if (field != null) or if (field.TryGetTarget(...))
      if (current is IfStatementSyntax ifStatement) {
        var condition = ifStatement.Condition.ToString();
        var fieldName = ((MemberAccessExpressionSyntax)memberAccess)
            .Name.Identifier.Text;

        if (condition.Contains(fieldName) &&
            (condition.Contains("!= null") ||
             condition.Contains("is not null") ||
             condition.Contains("TryGetTarget"))) {
          return true;
        }
      }

      current = current.Parent;
    }

    return false;
  }
}
