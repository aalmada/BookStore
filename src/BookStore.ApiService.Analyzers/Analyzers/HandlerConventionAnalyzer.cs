using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BookStore.ApiService.Analyzers.Analyzers;

/// <summary>
/// Analyzer to ensure handler methods follow Wolverine conventions (BS4001-BS4003)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class HandlerConventionAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor ShouldBeNamedHandleRule = new(
        id: DiagnosticIds.HandlerMethodShouldBeNamedHandle,
        title: "Handler methods should be named 'Handle'",
        messageFormat: "Handler method '{0}' should be named 'Handle' for Wolverine convention",
        category: DiagnosticCategories.CQRS,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Wolverine discovers handlers by the method name 'Handle'.");

    static readonly DiagnosticDescriptor ShouldBeStaticRule = new(
        id: DiagnosticIds.HandlerMethodShouldBeStatic,
        title: "Handler methods should be static",
        messageFormat: "Handler method '{0}' should be static for better performance",
        category: DiagnosticCategories.CQRS,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Static handler methods provide better performance in Wolverine.");

    static readonly DiagnosticDescriptor FirstParameterShouldBeCommandRule = new(
        id: DiagnosticIds.HandlerFirstParameterShouldBeCommand,
        title: "Handler first parameter should be a command type",
        messageFormat: "Handler method '{0}' first parameter should be from a Commands namespace",
        category: DiagnosticCategories.CQRS,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Wolverine routes messages based on the first parameter type.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => [ShouldBeNamedHandleRule, ShouldBeStaticRule, FirstParameterShouldBeCommandRule];

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
        {
            return;
        }

        // Only analyze methods in Handlers namespace
        if (!IsInHandlersNamespace(methodSymbol.ContainingType))
        {
            return;
        }

        // Only analyze public methods
        if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
        {
            return;
        }

        var methodName = methodSymbol.Name;

        // BS4001: Check if method is named "Handle"
        if (methodName != "Handle" && LooksLikeHandler(methodSymbol))
        {
            var diagnostic = Diagnostic.Create(
                ShouldBeNamedHandleRule,
                methodDeclaration.Identifier.GetLocation(),
                methodName);
            context.ReportDiagnostic(diagnostic);
        }

        // Only check the following rules for methods named "Handle"
        if (methodName != "Handle")
        {
            return;
        }

        // BS4002: Check if method is static
        if (!methodSymbol.IsStatic)
        {
            var diagnostic = Diagnostic.Create(
                ShouldBeStaticRule,
                methodDeclaration.Identifier.GetLocation(),
                methodName);
            context.ReportDiagnostic(diagnostic);
        }

        // BS4003: Check if first parameter is from Commands namespace
        if (methodSymbol.Parameters.Length > 0)
        {
            var firstParam = methodSymbol.Parameters[0];
            if (!IsFromCommandsNamespace(firstParam.Type))
            {
                var diagnostic = Diagnostic.Create(
                    FirstParameterShouldBeCommandRule,
                    methodDeclaration.Identifier.GetLocation(),
                    methodName);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    static bool IsInHandlersNamespace(INamedTypeSymbol symbol)
    {
        var namespaceName = symbol.ContainingNamespace?.ToDisplayString();
        return namespaceName?.EndsWith(".Handlers") == true;
    }

    static bool IsFromCommandsNamespace(ITypeSymbol type)
    {
        var namespaceName = type.ContainingNamespace?.ToDisplayString();
        return namespaceName?.Contains(".Commands") == true;
    }

    static bool LooksLikeHandler(IMethodSymbol method)
        // A method looks like a handler if it has at least one parameter
        // and returns a result type (IResult, Task<IResult>, etc.)
        => method.Parameters.Length > 0;
}
