using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BookStore.ApiService.Analyzers.Analyzers;

/// <summary>
/// Analyzer for aggregate-specific rules (BS3004, BS3005)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AggregateRulesAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor ShouldReturnEventRule = new(
        id: DiagnosticIds.AggregateCommandMethodShouldReturnEvent,
        title: "Aggregate command methods should return events",
        messageFormat: "Aggregate method '{0}' should return an event type instead of void",
        category: DiagnosticCategories.EventSourcing,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Aggregate command methods generate events for event sourcing and should return event types.");

    static readonly DiagnosticDescriptor ShouldNotHavePublicSetterRule = new(
        id: DiagnosticIds.AggregatePropertyShouldNotHavePublicSetter,
        title: "Aggregate properties should not have public setters",
        messageFormat: "Aggregate property '{0}' should not have a public setter - use 'init' or make it private",
        category: DiagnosticCategories.DomainModel,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Aggregate state changes should only occur through Apply methods, not direct property setters.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => [ShouldReturnEventRule, ShouldNotHavePublicSetterRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

        if (symbol == null || !IsInAggregatesNamespace(symbol))
        {
            return;
        }

        // BS3005: Check for properties with public setters
        foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.SetMethod != null &&
                member.SetMethod.DeclaredAccessibility == Accessibility.Public &&
                !member.SetMethod.IsInitOnly)
            {
                var propertySyntax = classDeclaration.Members
                    .OfType<PropertyDeclarationSyntax>()
                    .FirstOrDefault(p => p.Identifier.Text == member.Name);

                if (propertySyntax != null)
                {
                    var diagnostic = Diagnostic.Create(
                        ShouldNotHavePublicSetterRule,
                        propertySyntax.Identifier.GetLocation(),
                        member.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        // BS3004: Check for public methods that return void (excluding Apply methods)
        foreach (var member in symbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.DeclaredAccessibility == Accessibility.Public &&
                member.MethodKind == MethodKind.Ordinary &&
                member.Name != "Apply" &&
                !member.IsStatic &&
                member.ReturnsVoid)
            {
                var methodSyntax = classDeclaration.Members
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == member.Name);

                if (methodSyntax != null)
                {
                    var diagnostic = Diagnostic.Create(
                        ShouldReturnEventRule,
                        methodSyntax.Identifier.GetLocation(),
                        member.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    static bool IsInAggregatesNamespace(INamedTypeSymbol symbol)
    {
        var namespaceName = symbol.ContainingNamespace?.ToDisplayString();
        return namespaceName?.EndsWith(".Aggregates") == true;
    }
}
