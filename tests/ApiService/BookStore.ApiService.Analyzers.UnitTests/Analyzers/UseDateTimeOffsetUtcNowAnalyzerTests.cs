using BookStore.ApiService.Analyzers.Analyzers;
using BookStore.ApiService.Analyzers.UnitTests.Verifiers;
using Microsoft.CodeAnalysis.Testing;
using TUnit.Core;

namespace BookStore.ApiService.Analyzers.UnitTests.Analyzers;

public class UseDateTimeOffsetUtcNowAnalyzerTests
{
    [Test]
    [Arguments("TestData/BS1007/NoDiagnostic/ValidDateTimeUsage.cs")]
    public async Task Verify_NoDiagnostics(string path)
    {
        var source = await File.ReadAllTextAsync(path);
        await CSharpAnalyzerVerifier<UseDateTimeOffsetUtcNowAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Test]
    [Arguments("TestData/BS1007/Diagnostic/InvalidDateTimeNow.cs", 10, 19, "DateTime.Now")]
    [Arguments("TestData/BS1007/Diagnostic/InvalidDateTimeUtcNow.cs", 10, 19, "DateTime.UtcNow")]
    [Arguments("TestData/BS1007/Diagnostic/InvalidDateTimeInField.cs", 8, 35, "DateTime.Now")]
    public async Task Verify_Diagnostics(string path, int line, int column, string dateTimeUsage)
    {
        var source = await File.ReadAllTextAsync(path);

        var expected = CSharpAnalyzerVerifier<UseDateTimeOffsetUtcNowAnalyzer>
            .Diagnostic(DiagnosticIds.UseDateTimeOffsetUtcNow)
            .WithLocation(line, column)
            .WithArguments(dateTimeUsage);

        await CSharpAnalyzerVerifier<UseDateTimeOffsetUtcNowAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }
}
