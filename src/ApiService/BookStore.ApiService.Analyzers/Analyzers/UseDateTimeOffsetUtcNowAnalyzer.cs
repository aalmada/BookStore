using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BookStore.ApiService.Analyzers.Analyzers;

/// <summary>
/// Analyzer to enforce the use of DateTimeOffset.UtcNow instead of DateTime.Now (BS1007)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UseDateTimeOffsetUtcNowAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.UseDateTimeOffsetUtcNow,
        title: "Use DateTimeOffset.UtcNow instead of DateTime.Now",
        messageFormat: "Use 'DateTimeOffset.UtcNow' instead of '{0}' for timezone-aware timestamps",
        category: DiagnosticCategories.BestPractices,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "DateTimeOffset.UtcNow provides timezone-aware timestamps and is preferred over DateTime.Now or DateTime.UtcNow for distributed systems and event sourcing.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Check if this is accessing Now or UtcNow
        var memberName = memberAccess.Name.Identifier.Text;
        if (memberName != "Now" && memberName != "UtcNow")
        {
            return;
        }

        // Get the symbol information
        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IPropertySymbol propertySymbol)
        {
            return;
        }

        // Check if this is DateTime.Now or DateTime.UtcNow
        var containingType = propertySymbol.ContainingType?.ToDisplayString();
        if (containingType == "System.DateTime")
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                memberAccess.GetLocation(),
                $"DateTime.{memberName}");
            context.ReportDiagnostic(diagnostic);
        }
    }
}
