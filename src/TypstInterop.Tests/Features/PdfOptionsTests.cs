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
        var result = compiler.Compile(c => c
            .WithSource("#set document(date: datetime(year: 2020, month: 1, day: 1))\n= Archival")
            .WithPdfStandard(TypstPdfStandard.A2b)
            .WithCreationTimestamp(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(0x25, result.Output.Span[0]);
    }

    [Fact]
    public void Compile_WithFixedTimestamp_ProducesDeterministicOutput()
    {
        using var compiler = new TypstCompiler();
        var timestamp = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var first = compiler.Compile(c => c.WithSource("= Deterministic").WithCreationTimestamp(timestamp));
        var second = compiler.Compile(c => c.WithSource("= Deterministic").WithCreationTimestamp(timestamp));

        Assert.True(first.IsSuccess, first.ErrorMessage);
        Assert.True(second.IsSuccess, second.ErrorMessage);
        Assert.Equal(first.Output.ToArray(), second.Output.ToArray());
    }

    [Fact]
    public void Compile_WithMetadata_EmbedsTitle()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c
            .WithSource("= Body")
            .WithTitle("My Unique Title")
            .WithAuthor("Tester"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        // The title appears in the PDF metadata (single-byte-encoded in the file).
        var text = Encoding.GetEncoding("ISO-8859-1").GetString(result.Output.ToArray());
        Assert.Contains("My Unique Title", text);
    }
}
