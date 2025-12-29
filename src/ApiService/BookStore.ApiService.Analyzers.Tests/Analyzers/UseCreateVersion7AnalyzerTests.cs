using BookStore.ApiService.Analyzers.Analyzers;
using Microsoft.CodeAnalysis.Testing;
using TUnit.Core;

namespace BookStore.ApiService.Analyzers.Tests.Analyzers;

public class UseCreateVersion7AnalyzerTests
{
    [Test]
    [Arguments("TestData/BS1006/NoDiagnostic/ValidGuidUsage.cs")]
    public async Task Verify_NoDiagnostics(string path)
    {
        var source = await File.ReadAllTextAsync(path);
        await CSharpAnalyzerVerifier<UseCreateVersion7Analyzer>.VerifyAnalyzerAsync(source);
    }

    [Test]
    [Arguments("TestData/BS1006/Diagnostic/InvalidGuidInMethod.cs", 10, 18)]
    [Arguments("TestData/BS1006/Diagnostic/InvalidGuidInField.cs", 8, 24)]
    [Arguments("TestData/BS1006/Diagnostic/InvalidGuidInProperty.cs", 8, 37)]
    public async Task Verify_Diagnostics(string path, int line, int column)
    {
        var source = await File.ReadAllTextAsync(path);
        
        var expected = CSharpAnalyzerVerifier<UseCreateVersion7Analyzer>
            .Diagnostic(DiagnosticIds.UseCreateVersion7)
            .WithLocation(line, column);

        await CSharpAnalyzerVerifier<UseCreateVersion7Analyzer>.VerifyAnalyzerAsync(source, expected);
    }
}
