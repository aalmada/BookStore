using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BookStore.ApiService.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseGenericMathCodeFixProvider)), Shared]
public class UseGenericMathCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [DiagnosticIds.UseGenericMath];

    public sealed override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var memberAccess = root.FindNode(diagnosticSpan).FirstAncestorOrSelf<MemberAccessExpressionSyntax>();
        if (memberAccess == null)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.Text;
        
        // We need to determine the type to replace 'Math' with.
        // We can re-evaluate the semantics or trust the analyzer to be correct and re-calculate the type from the argument.
        // However, the analyzer context isn't fully available here without re-doing semantic analysis.
        
        // Let's get the semantic model to find the argument type again.
        // This is safe because we are in the code fix action.
        
        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Use generic math",
                createChangedDocument: c => ReplaceMathWithGenericAsync(context.Document, memberAccess, c),
                equivalenceKey: nameof(UseGenericMathCodeFixProvider)),
            diagnostic);
    }

    private async Task<Document> ReplaceMathWithGenericAsync(Document document, MemberAccessExpressionSyntax memberAccess, CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document;
        }

        // Parent should be InvocationExpression
        if (memberAccess.Parent is not InvocationExpressionSyntax invocation)
        {
            return document;
        }

        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return document;
        }

        var firstArg = invocation.ArgumentList.Arguments[0];
        var typeInfo = semanticModel.GetTypeInfo(firstArg.Expression, cancellationToken);
        var type = typeInfo.Type;

        if (type == null)
        {
            return document;
        }

        var typeSyntax = GetTypeSyntax(type);
        
        // Create new member access: Type.Method
        // memberAccess.Name is the method name (e.g. Max)
        var newMemberAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            typeSyntax,
            memberAccess.Name)
            .WithLeadingTrivia(memberAccess.GetLeadingTrivia())
            .WithTrailingTrivia(memberAccess.GetTrailingTrivia());

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }
        
        var newRoot = root.ReplaceNode(memberAccess, newMemberAccess);
        return document.WithSyntaxRoot(newRoot);
    }

    private TypeSyntax GetTypeSyntax(ITypeSymbol type)
    {
        // Use predefined types (int, double, etc) if available
         switch (type.SpecialType)
        {
            case SpecialType.System_Int32: return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword));
            case SpecialType.System_Double: return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.DoubleKeyword));
            case SpecialType.System_Single: return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.FloatKeyword));
            case SpecialType.System_Decimal: return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.DecimalKeyword));
            case SpecialType.System_Int64: return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.LongKeyword));
            case SpecialType.System_Int16: return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ShortKeyword));
            case SpecialType.System_Byte: return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ByteKeyword));
            case SpecialType.System_SByte: return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.SByteKeyword));
            case SpecialType.System_UInt16: return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.UShortKeyword));
            case SpecialType.System_UInt32: return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.UIntKeyword));
            case SpecialType.System_UInt64: return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ULongKeyword));
            default: return SyntaxFactory.ParseTypeName(type.ToMinimalDisplayString(null!, 0)); // Fallback, though we shouldn't hit this for supported types
        }
    }
}
