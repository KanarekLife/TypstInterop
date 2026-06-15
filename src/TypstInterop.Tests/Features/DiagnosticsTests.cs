using System.Linq;
using TypstInterop.Models;
using Xunit;

namespace TypstInterop.Tests.Features;

public class DiagnosticsTests
{
    [Fact]
    public void Compile_Error_ExposesStructuredDiagnostic()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c.WithSource("#non_existent()"));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(TypstDiagnosticSeverity.Error, error.Severity);
        Assert.Contains("unknown variable: non_existent", error.Message);
        // The error points into the main source file.
        Assert.Equal("main.typ", error.FilePath);
        Assert.True(error.Line >= 1);
        Assert.True(error.Column >= 1);
    }

    [Fact]
    public void Compile_Warning_IsSurfacedOnSuccess()
    {
        using var compiler = new TypstCompiler();
        // HTML export always emits the "under active development" warning, which
        // gives us a stable way to verify warnings are surfaced on success.
        var result = compiler.Compile(
            new TypstCompileOptions { Format = TypstOutputFormat.Html },
            c => c.WithSource("= Hello"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotEmpty(result.Warnings);
        Assert.All(result.Warnings, w => Assert.Equal(TypstDiagnosticSeverity.Warning, w.Severity));
    }

    [Fact]
    public void Compile_Success_HasNoErrors()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c.WithSource("= All good"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Diagnostic_ToString_IncludesLocation()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c.WithSource("#non_existent()"));

        var error = result.Errors.First();
        Assert.Contains("main.typ", error.ToString());
        Assert.Contains("error", error.ToString());
    }
}
