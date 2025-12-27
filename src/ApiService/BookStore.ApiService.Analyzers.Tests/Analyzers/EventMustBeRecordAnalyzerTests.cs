using BookStore.ApiService.Analyzers.Analyzers;
using Microsoft.CodeAnalysis.Testing;
using TUnit.Core;
using TUnit.Assertions;

namespace BookStore.ApiService.Analyzers.Tests.Analyzers;

public class EventMustBeRecordAnalyzerTests
{
    [Test]
    [Arguments("TestData/BS1001/NoDiagnostic/ValidEvents.cs")]
    public async Task Verify_NoDiagnostics(string path)
    {
        var source = await File.ReadAllTextAsync(path);
        await CSharpAnalyzerVerifier<EventMustBeRecordAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Test]
    [Arguments("TestData/BS1001/Diagnostic/BookAddedClass.cs", "BookAdded", 6, 14)]
    [Arguments("TestData/BS1001/Diagnostic/AuthorUpdatedClass.cs", "AuthorUpdated", 6, 14)]
    public async Task Verify_Diagnostics(string path, string typeName, int line, int column)
    {
        var source = await File.ReadAllTextAsync(path);
        
        var expected = CSharpAnalyzerVerifier<EventMustBeRecordAnalyzer>
            .Diagnostic(DiagnosticIds.EventMustBeRecord)
            .WithLocation(line, column)
            .WithArguments(typeName);

        await CSharpAnalyzerVerifier<EventMustBeRecordAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }
}
