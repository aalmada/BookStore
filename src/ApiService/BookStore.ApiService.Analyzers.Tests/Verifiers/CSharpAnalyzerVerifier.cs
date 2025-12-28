using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace BookStore.ApiService.Analyzers.Tests;

/// <summary>
/// Base class for diagnostic analyzer tests using MSTest
/// Based on https://aalmada.github.io/posts/Unit-testing-a-Roslyn-Analyzer/
/// </summary>
public static class CSharpAnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <summary>
    /// Verifies that the given source code produces no diagnostics
    /// </summary>
    public static async Task VerifyAnalyzerAsync(string source) => await VerifyAnalyzerAsync(source, []);

    /// <summary>
    /// Verifies that the given source code produces the expected diagnostics
    /// </summary>
    public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new Test
        {
            TestCode = source,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }

    /// <summary>
    /// Helper to create a DiagnosticResult for a specific location
    /// </summary>
    public static DiagnosticResult Diagnostic(string diagnosticId) => new(diagnosticId, DiagnosticSeverity.Warning);

    class Test : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    {
        public Test() => ReferenceAssemblies = ReferenceAssemblies.Net.Net90; // Use the latest C# language version
    }
}
