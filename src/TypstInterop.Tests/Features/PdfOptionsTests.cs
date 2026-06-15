using System;
using System.Text;
using TypstInterop.Models;
using Xunit;

namespace TypstInterop.Tests.Features;

public class PdfOptionsTests
{
    [Fact]
    public void Compile_WithPdfAStandard_Succeeds()
    {
        using var compiler = new TypstCompiler();
        // PDF/A requires a document date; supply one via the creation timestamp.
        var result = compiler.Compile(
            new TypstCompileOptions
            {
                PdfStandard = TypstPdfStandard.A2b,
                CreationTimestamp = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            c => c.WithSource("#set document(date: datetime(year: 2020, month: 1, day: 1))\n= Archival"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(0x25, result.GetBytes()[0]);
    }

    [Fact]
    public void Compile_WithFixedTimestamp_ProducesDeterministicOutput()
    {
        using var compiler = new TypstCompiler();
        var options = new TypstCompileOptions
        {
            CreationTimestamp = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

        var first = compiler.Compile(options, c => c.WithSource("= Deterministic"));
        var second = compiler.Compile(options, c => c.WithSource("= Deterministic"));

        Assert.True(first.IsSuccess, first.ErrorMessage);
        Assert.True(second.IsSuccess, second.ErrorMessage);
        Assert.Equal(first.GetBytes(), second.GetBytes());
    }

    [Fact]
    public void Compile_WithMetadata_EmbedsTitle()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(
            new TypstCompileOptions { Title = "My Unique Title", Author = "Tester" },
            c => c.WithSource("= Body"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        // The title appears in the PDF metadata (single-byte-encoded in the file).
        var text = Encoding.GetEncoding("ISO-8859-1").GetString(result.GetBytes());
        Assert.Contains("My Unique Title", text);
    }
}
