using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BookStore.ApiService.Analyzers.Analyzers;

/// <summary>
/// Analyzer to ensure events follow event sourcing patterns (BS1001, BS1002, BS1003)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EventMustBeRecordAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor MustBeRecordRule = new(
        id: DiagnosticIds.EventMustBeRecord,
        title: "Events must be declared as record types",
        messageFormat: "Event '{0}' should be declared as a record type for immutability",
        category: DiagnosticCategories.EventSourcing,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Events represent immutable historical facts in event sourcing and should be declared as record types.");

    static readonly DiagnosticDescriptor MustBeImmutableRule = new(
        id: DiagnosticIds.EventMustBeImmutable,
        title: "Event properties must be immutable",
        messageFormat: "Event property '{0}' in '{1}' should not have a mutable setter",
        category: DiagnosticCategories.EventSourcing,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Event properties must be immutable to preserve historical integrity.");

    static readonly DiagnosticDescriptor MustBeInEventsNamespaceRule = new(
        id: DiagnosticIds.EventMustBeInEventsNamespace,
        title: "Events must be in Events namespace",
        messageFormat: "Event type '{0}' should be in a namespace ending with '.Events'",
        category: DiagnosticCategories.Architecture,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Events should be organized in namespaces ending with '.Events' for consistency.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => [MustBeRecordRule, MustBeImmutableRule, MustBeInEventsNamespaceRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeRecordDeclaration, SyntaxKind.RecordDeclaration);
    }

    static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

        if (symbol == null)
        {
            return;
        }

        // BS1001: Check if class in Events namespace should be a record
        if (IsInEventsNamespace(symbol) && IsEventType(symbol.Name))
        {
            var diagnostic = Diagnostic.Create(MustBeRecordRule, classDeclaration.Identifier.GetLocation(), symbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        // BS1003: Check if event-like type is in correct namespace
        if (!IsInEventsNamespace(symbol) && IsEventType(symbol.Name))
        {
            var diagnostic = Diagnostic.Create(MustBeInEventsNamespaceRule, classDeclaration.Identifier.GetLocation(), symbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    static void AnalyzeRecordDeclaration(SyntaxNodeAnalysisContext context)
    {
        var recordDeclaration = (RecordDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(recordDeclaration);

        if (symbol == null)
        {
            return;
        }

        // BS1003: Check if event record is in correct namespace
        if (!IsInEventsNamespace(symbol) && IsEventType(symbol.Name))
        {
            var diagnostic = Diagnostic.Create(MustBeInEventsNamespaceRule, recordDeclaration.Identifier.GetLocation(), symbol.Name);
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // BS1002: Check for mutable properties in event records
        if (IsInEventsNamespace(symbol))
        {
            foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
            {
                // Check if property has a public setter (not init-only)
                if (member.SetMethod != null &&
                    member.SetMethod.DeclaredAccessibility == Accessibility.Public &&
                    !member.SetMethod.IsInitOnly)
                {
                    var propertySyntax = recordDeclaration.Members
                        .OfType<PropertyDeclarationSyntax>()
                        .FirstOrDefault(p => p.Identifier.Text == member.Name);

                    if (propertySyntax != null)
                    {
                        var diagnostic = Diagnostic.Create(
                            MustBeImmutableRule,
                            propertySyntax.Identifier.GetLocation(),
                            member.Name,
                            symbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }

    static bool IsInEventsNamespace(INamedTypeSymbol symbol)
    {
        var namespaceName = symbol.ContainingNamespace?.ToDisplayString();
        return namespaceName?.EndsWith(".Events") == true;
    }

    static bool IsEventType(string typeName) => typeName.EndsWith("Added") ||
               typeName.EndsWith("Updated") ||
               typeName.EndsWith("Deleted") ||
               typeName.EndsWith("Restored") ||
               typeName.EndsWith("Changed") ||
               typeName.EndsWith("Created") ||
               typeName.EndsWith("Removed") ||
               typeName.EndsWith("Event");
}
