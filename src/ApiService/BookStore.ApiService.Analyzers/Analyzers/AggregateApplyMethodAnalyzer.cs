using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace BookStore.ApiService.Analyzers.Analyzers;

/// <summary>
/// Analyzer to ensure aggregate Apply methods return void (BS3001)
/// and have exactly one parameter (BS3002)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AggregateApplyMethodAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor ReturnVoidRule = new(
        id: DiagnosticIds.ApplyMethodMustReturnVoid,
        title: "Apply methods must return void",
        messageFormat: "Apply method in aggregate '{0}' must return void (Marten convention)",
        category: DiagnosticCategories.EventSourcing,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Marten requires Apply methods to return void for event application.");

    static readonly DiagnosticDescriptor OneParameterRule = new(
        id: DiagnosticIds.ApplyMethodMustHaveOneParameter,
        title: "Apply methods must have exactly one parameter",
        messageFormat: "Apply method in aggregate '{0}' must have exactly one parameter (the event)",
        category: DiagnosticCategories.EventSourcing,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Marten requires Apply methods to have exactly one parameter representing the event.");

    static readonly DiagnosticDescriptor ShouldBePrivateRule = new(
        id: DiagnosticIds.ApplyMethodShouldBePrivate,
        title: "Apply methods should be private or internal",
        messageFormat: "Apply method in aggregate '{0}' should be private or internal (called by Marten)",
        category: DiagnosticCategories.EventSourcing,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Apply methods are called by Marten during rehydration and should not be public.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(ReturnVoidRule, OneParameterRule, ShouldBePrivateRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
    }

    static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

        if (methodSymbol == null)
            return;

        // Only analyze methods named "Apply"
        if (methodSymbol.Name != "Apply")
            return;

        // Only analyze methods in Aggregates namespace
        if (!IsInAggregatesNamespace(methodSymbol.ContainingType))
            return;

        var aggregateName = methodSymbol.ContainingType.Name;

        // BS3001: Check return type
        if (methodSymbol.ReturnsVoid == false)
        {
            var diagnostic = Diagnostic.Create(
                ReturnVoidRule,
                methodDeclaration.ReturnType.GetLocation(),
                aggregateName);
            context.ReportDiagnostic(diagnostic);
        }

        // BS3002: Check parameter count
        if (methodSymbol.Parameters.Length != 1)
        {
            var diagnostic = Diagnostic.Create(
                OneParameterRule,
                methodDeclaration.Identifier.GetLocation(),
                aggregateName);
            context.ReportDiagnostic(diagnostic);
        }

        // BS3003: Check accessibility
        if (methodSymbol.DeclaredAccessibility == Accessibility.Public)
        {
            var diagnostic = Diagnostic.Create(
                ShouldBePrivateRule,
                methodDeclaration.Identifier.GetLocation(),
                aggregateName);
            context.ReportDiagnostic(diagnostic);
        }
    }

    static bool IsInAggregatesNamespace(INamedTypeSymbol symbol)
    {
        var namespaceName = symbol.ContainingNamespace?.ToDisplayString();
        return namespaceName != null && namespaceName.EndsWith(".Aggregates");
    }
}
