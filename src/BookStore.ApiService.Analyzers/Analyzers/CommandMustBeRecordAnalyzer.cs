using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BookStore.ApiService.Analyzers.Analyzers;

/// <summary>
/// Analyzer to ensure commands follow CQRS patterns (BS2001, BS2002, BS2003)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CommandMustBeRecordAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor MustBeRecordRule = new(
        id: DiagnosticIds.CommandMustBeRecord,
        title: "Commands must be declared as record types",
        messageFormat: "Command '{0}' should be declared as a record type for immutability",
        category: DiagnosticCategories.CQRS,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Commands are immutable DTOs in CQRS and should be declared as record types.");

    static readonly DiagnosticDescriptor MustBeInCommandsNamespaceRule = new(
        id: DiagnosticIds.CommandMustBeInCommandsNamespace,
        title: "Commands must be in the Commands namespace",
        messageFormat: "Command '{0}' should be in a namespace ending with '.Commands'",
        category: DiagnosticCategories.Architecture,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Commands should be organized in the Commands namespace for consistent architecture.");

    static readonly DiagnosticDescriptor ShouldUseInitRule = new(
        id: DiagnosticIds.CommandPropertiesShouldUseInit,
        title: "Command properties should use init accessors",
        messageFormat: "Command property '{0}' in '{1}' should use 'init' instead of 'set' for immutability",
        category: DiagnosticCategories.CQRS,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Command properties should use init-only setters to ensure immutability after construction.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => [MustBeRecordRule, MustBeInCommandsNamespaceRule, ShouldUseInitRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.ClassDeclaration, SyntaxKind.RecordDeclaration);
    }

    static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);

        if (symbol == null)
        {
            return;
        }

        var isCommandType = IsCommandType(symbol.Name);
        var isInCommandsNamespace = IsInCommandsNamespace(symbol);

        // BS2001: If it's in Commands namespace, it should be a record
        if (isInCommandsNamespace && typeDeclaration is ClassDeclarationSyntax classDecl && !classDecl.Modifiers.Any(SyntaxKind.RecordKeyword))
        {
            var diagnostic = Diagnostic.Create(MustBeRecordRule, typeDeclaration.Identifier.GetLocation(), symbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        // BS2002: If it looks like a command, it should be in Commands namespace
        if (isCommandType && !isInCommandsNamespace)
        {
            var diagnostic = Diagnostic.Create(MustBeInCommandsNamespaceRule, typeDeclaration.Identifier.GetLocation(), symbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        // BS2003: Check for properties with 'set' instead of 'init' in command records
        if (isInCommandsNamespace && typeDeclaration is RecordDeclarationSyntax)
        {
            foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
            {
                // Check if property has a public setter that is not init-only
                if (member.SetMethod != null &&
                    member.SetMethod.DeclaredAccessibility == Accessibility.Public &&
                    !member.SetMethod.IsInitOnly)
                {
                    var propertySyntax = typeDeclaration.Members
                        .OfType<PropertyDeclarationSyntax>()
                        .FirstOrDefault(p => p.Identifier.Text == member.Name);

                    if (propertySyntax != null)
                    {
                        var diagnostic = Diagnostic.Create(
                            ShouldUseInitRule,
                            propertySyntax.Identifier.GetLocation(),
                            member.Name,
                            symbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }

    static bool IsInCommandsNamespace(INamedTypeSymbol symbol)
    {
        var namespaceName = symbol.ContainingNamespace?.ToDisplayString();
        return namespaceName?.EndsWith(".Commands") == true;
    }

    static bool IsCommandType(string typeName) => typeName.StartsWith("Create") ||
               typeName.StartsWith("Update") ||
               typeName.StartsWith("Delete") ||
               typeName.StartsWith("Restore") ||
               typeName.StartsWith("Remove") ||
               typeName.EndsWith("Command");
}
