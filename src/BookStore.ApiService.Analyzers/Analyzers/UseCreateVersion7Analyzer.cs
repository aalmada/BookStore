using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BookStore.ApiService.Analyzers.Analyzers;

/// <summary>
/// Analyzer to enforce the use of Guid.CreateVersion7() instead of Guid.NewGuid() (BS1006)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UseCreateVersion7Analyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.UseCreateVersion7,
        title: "Use Guid.CreateVersion7() instead of Guid.NewGuid()",
        messageFormat: "Use 'Guid.CreateVersion7()' instead of 'Guid.NewGuid()' for time-ordered GUIDs",
        category: DiagnosticCategories.BestPractices,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Guid.CreateVersion7() creates time-ordered UUIDs (version 7) which provide better database performance and natural sorting. Use this instead of Guid.NewGuid() which creates random UUIDs (version 4).");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check if this is a member access expression (e.g., Guid.NewGuid())
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        // Check if the method name is "NewGuid"
        if (memberAccess.Name.Identifier.Text != "NewGuid")
        {
            return;
        }

        // Get the symbol information
        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        // Check if this is Guid.NewGuid()
        if (methodSymbol.ContainingType?.ToDisplayString() == "System.Guid" &&
            methodSymbol.Name == "NewGuid")
        {
            var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
