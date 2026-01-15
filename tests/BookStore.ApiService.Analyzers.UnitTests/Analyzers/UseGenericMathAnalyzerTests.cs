using BookStore.ApiService.Analyzers.Analyzers;
using BookStore.ApiService.Analyzers.CodeFixes;
using BookStore.ApiService.Analyzers.UnitTests.Verifiers;
using Microsoft.CodeAnalysis.Testing;
using TUnit.Core;

namespace BookStore.ApiService.Analyzers.UnitTests.Analyzers;

using Verify = CSharpCodeFixVerifier<UseGenericMathAnalyzer, UseGenericMathCodeFixProvider>;

public class UseGenericMathAnalyzerTests
{
    [Test]
    public async Task MathMax_Int_ShouldReportDiagnostic()
    {
        var code = @"
using System;

class Program
{
    void M()
    {
        var x = Math.Max(1, 2);
    }
}";

        var fixedCode = @"
using System;

class Program
{
    void M()
    {
        var x = int.Max(1, 2);
    }
}";

        await Verify.VerifyCodeFixAsync(code, CreateDiagnostic("Max", "int", 8, 17), fixedCode);
    }

    [Test]
    public async Task MathMin_Double_ShouldReportDiagnostic()
    {
        var code = @"
using System;

class Program
{
    void M()
    {
        var x = Math.Min(1.0, 2.0);
    }
}";

        var fixedCode = @"
using System;

class Program
{
    void M()
    {
        var x = double.Min(1.0, 2.0);
    }
}";

        await Verify.VerifyCodeFixAsync(code, CreateDiagnostic("Min", "double", 8, 17), fixedCode);
    }

    [Test]
    public async Task MathAbs_Decimal_ShouldReportDiagnostic()
    {
        var code = @"
using System;

class Program
{
    void M()
    {
        decimal d = 1.5m;
        var x = Math.Abs(d);
    }
}";

        var fixedCode = @"
using System;

class Program
{
    void M()
    {
        decimal d = 1.5m;
        var x = decimal.Abs(d);
    }
}";

        await Verify.VerifyCodeFixAsync(code, CreateDiagnostic("Abs", "decimal", 9, 17), fixedCode);
    }

    [Test]
    public async Task SystemMathMax_Int_ShouldReportDiagnostic()
    {
        var code = @"
class Program
{
    void M()
    {
        var x = System.Math.Max(1, 2);
    }
}";
        var fixedCode = @"
class Program
{
    void M()
    {
        var x = int.Max(1, 2);
    }
}";
        await Verify.VerifyCodeFixAsync(code, CreateDiagnostic("Max", "int", 6, 17), fixedCode);
    }

    [Test]
    public async Task MathPow_Double_ShouldReportDiagnostic()
    {
        var code = @"
using System;

class Program
{
    void M()
    {
        var x = Math.Pow(2.0, 3.0);
    }
}";

        var fixedCode = @"
using System;

class Program
{
    void M()
    {
        var x = double.Pow(2.0, 3.0);
    }
}";

        await Verify.VerifyCodeFixAsync(code, CreateDiagnostic("Pow", "double", 8, 17), fixedCode);
    }

    [Test]
    public async Task MathSqrt_Int_ShouldNotReportDiagnostic()
    {
        // int does not have Sqrt definition (only valid on floating point types of IRootFunctions)
        var code = @"
using System;

class Program
{
    void M()
    {
        var x = Math.Sqrt(4); // passed as int, converted to double
    }
}";

        await Verify.VerifyAnalyzerAsync(code);
    }

    static DiagnosticResult CreateDiagnostic(string method, string typeName, int line, int column) => Verify.Diagnostic(DiagnosticIds.UseGenericMath)
            .WithLocation(line, column)
            .WithArguments(typeName, method);
}
