using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BookStore.ApiService.Analyzers.Analyzers;

/// <summary>
/// Analyzer to enforce the use of Generic Math instead of System.Math (BS1008)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UseGenericMathAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.UseGenericMath,
        title: "Use generic math instead of System.Math",
        messageFormat: "Use '{0}.{1}' instead of 'Math.{1}'",
        category: DiagnosticCategories.BestPractices,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Generic math provides better performance and type safety than System.Math.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        // Check if invoking on System.Math
        // We do a quick check on the textual representation first to avoid expensive symbol lookups
        if (memberAccess.Expression.ToString() is not "Math" and not "System.Math")
        {
            // It might be a static using, or alias.
            // Symbol check below will confirm.
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess.Expression, context.CancellationToken);
        var typeSymbol = symbolInfo.Symbol as ITypeSymbol;

        if (typeSymbol?.ToDisplayString() != "System.Math")
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.Text;

        // Check argument types
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        var firstArgument = invocation.ArgumentList.Arguments[0];
        var argumentTypeInfo = context.SemanticModel.GetTypeInfo(firstArgument.Expression, context.CancellationToken);
        var argumentType = argumentTypeInfo.Type;

        if (argumentType == null || argumentType.TypeKind == TypeKind.Error)
        {
            return;
        }

        if (!HasStaticMethod(argumentType, methodName))
        {
            return;
        }

        var diagnostic = Diagnostic.Create(
            Rule,
            memberAccess.GetLocation(),
            argumentType.ToDisplayString(),
            methodName);

        context.ReportDiagnostic(diagnostic);
    }

    bool HasStaticMethod(ITypeSymbol typeSymbol, string methodName)
        // Check if the type has a static method with the given name
        => typeSymbol.GetMembers(methodName)
            .Any(m => m.IsStatic && m.Kind == SymbolKind.Method);
}
