using System;
using System.IO;
using System.Text;
using TypstInterop.Abstractions;
using TypstInterop.Models;
using Xunit;

namespace TypstInterop.Tests.Core;

public class CompilationTests
{
    [Fact]
    public void Version_ReturnsCorrectValue()
    {
        Assert.Equal("0.14.2", TypstCompiler.TypstVersion);
    }

    [Fact]
    public void Compile_SimplePdf_Succeeds()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(x => x.WithSource("= Hello World"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.GetBytes());
        Assert.NotEmpty(result.GetBytes());
        Assert.True(result.GetBytes().Length > 100, "PDF should have some content");
        Assert.Equal(0x25, result.GetBytes()[0]); // %
    }

    [Fact]
    public void Compile_InvalidSource_ReturnsError()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(x => x.WithSource("#non_existent()"));

        Assert.False(result.IsSuccess);
        Assert.Contains("unknown variable: non_existent", result.ErrorMessage);
    }

    [Fact]
    public void Compile_MultipleSources_Succeeds()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c
            .WithFile("other.typ", "#let x = [Value from other]")
            .WithSource(
                """
                            #import "other.typ": x
                            Main content: #x
                """
            ));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.GetBytes());
    }

    [Fact]
    public void Compile_StateIsReset_BetweenCalls()
    {
        using var compiler = new TypstCompiler();

        // 1. First compile with input
        compiler.Compile(c => c
            .WithInput("val", "First")
            .WithSource("#sys.inputs.val"));

        // 2. Second compile without input - should fail if state is NOT reset
        var result = compiler.Compile(c => c.WithSource("#sys.inputs.val"));

        Assert.False(result.IsSuccess);
        Assert.Contains("dictionary does not contain key \"val\"", result.ErrorMessage);
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        var compiler = new TypstCompiler();
        compiler.Dispose();
        compiler.Dispose();
    }
}
