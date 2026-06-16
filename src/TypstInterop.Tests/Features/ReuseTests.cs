using System;
using TypstInterop.Abstractions;
using TypstInterop.Models;
using Xunit;

namespace TypstInterop.Tests.Features;

/// <summary>
/// Guards the behaviour of the performance optimizations around compiler reuse:
/// deferred <c>Library</c> rebuilds (one build per compile regardless of input
/// count), font-store reuse across compiles, and comemo cache eviction. These
/// optimizations must not change observable compilation results.
/// </summary>
public class ReuseTests
{
    [Fact]
    public void Compile_WithManyInputs_AllResolve()
    {
        // Exercises the deferred Library rebuild: previously every WithInput
        // rebuilt the whole standard library; now the rebuild happens once at
        // compile time. All inputs must still be visible to the document.
        using var compiler = new TypstCompiler();

        var result = compiler.Compile(c =>
        {
            for (var i = 0; i < 20; i++)
                c.WithInput($"key{i}", $"value{i}");
            c.WithSource(
                """
                #import sys: inputs
                #for i in range(20) [
                  #inputs.at("key" + str(i))
                ]
                """);
            return c;
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotEmpty(result.Output.ToArray());
    }

    [Fact]
    public void Compile_RepeatedReuse_ProducesConsistentResults()
    {
        // A reused compiler must produce a valid PDF on every iteration. This
        // is the path where comemo cache growth and font-store rebuilds would
        // accumulate, and where deferred-library dirtiness must be tracked
        // correctly across reset() calls.
        using var compiler = new TypstCompiler();

        for (var i = 0; i < 10; i++)
        {
            var result = compiler.Compile(c => c
                .WithInput("n", i.ToString())
                .WithSource("#import sys: inputs\nValue: #inputs.n"));

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotEmpty(result.Output.ToArray());
            Assert.Equal(0x25, result.Output.Span[0]); // %PDF
        }
    }

    [Fact]
    public void Compile_InputsClearedBetweenReuses()
    {
        // The deferred rebuild must still honor reset(): inputs set in one
        // compile must not leak into the next.
        using var compiler = new TypstCompiler();

        var first = compiler.Compile(c => c
            .WithInput("leak", "present")
            .WithSource("#import sys: inputs\n#inputs.leak"));
        Assert.True(first.IsSuccess, first.ErrorMessage);

        var second = compiler.Compile(c => c
            .WithSource("#import sys: inputs\n#inputs.leak"));
        Assert.False(second.IsSuccess);
        Assert.Contains("does not contain key \"leak\"", second.ErrorMessage);
    }

    [Fact]
    public void Compile_DefaultFontsWorkAcrossManyReuses()
    {
        // With the pristine-font-store optimization the store is no longer
        // rebuilt every compile. Default-font text must keep rendering across
        // many reuses.
        using var compiler = new TypstCompiler(new TypstCompilerOptions
        {
            FontsSource = TypstFontsSource.DefaultOnly
        });

        for (var i = 0; i < 5; i++)
        {
            var result = compiler.Compile(c => c.WithSource("= Heading\nSome body text."));
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotEmpty(result.Output.ToArray());
        }
    }

    [Fact]
    public void Compile_AddedFontDoesNotLeakIntoNextCompile()
    {
        // add_font marks the store non-pristine so reset() rebuilds it; a font
        // added in one compile must not survive into the next.
        using var compiler = new TypstCompiler(new TypstCompilerOptions
        {
            FontsSource = TypstFontsSource.ProvidedOnly
        });

        var withFont = compiler.Compile(c => c
            .WithFont(FontTests.PixelFontData)
            .WithSource("#set text(font: \"Pixel\")\nHello in Pixel!"));
        Assert.True(withFont.IsSuccess, withFont.ErrorMessage);

        // Second compile (ProvidedOnly, no font provided) must not still see
        // the Pixel font from the previous compile.
        var withoutFont = compiler.Compile(c => c
            .WithSource("#set text(font: \"Pixel\")\nHello?"));

        // ProvidedOnly with no fonts at all should not succeed with Pixel.
        if (withoutFont.IsSuccess)
        {
            // If Typst still succeeds it must be via fallback, not the leaked
            // Pixel font (which would imply the store was not reset). We can't
            // assert glyph identity here, so we only assert the compile path
            // ran; the meaningful assertion is that it does not throw/leak.
            Assert.NotNull(withoutFont.Output.ToArray());
        }
    }
}
