using System.Linq;
using System.Text;
using TypstInterop.Models;
using Xunit;

namespace TypstInterop.Tests.Features;

public class OutputFormatTests
{
    [Fact]
    public void Compile_Png_ProducesPngPerPage()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c
            .WithSource("= Page One\n#pagebreak()\n= Page Two")
            .WithFormat(TypstOutputFormat.Png)
            .WithPpi(96));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2, result.Outputs.Count);
        foreach (var page in result.Outputs)
        {
            Assert.True(page.Length > 0);
            // PNG magic number: 0x89 'P' 'N' 'G'
            Assert.Equal(0x89, page.Span[0]);
            Assert.Equal((byte)'P', page.Span[1]);
            Assert.Equal((byte)'N', page.Span[2]);
            Assert.Equal((byte)'G', page.Span[3]);
        }
    }

    [Fact]
    public void Compile_Svg_ProducesSvgPerPage()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c
            .WithSource("= Hello\n#pagebreak()\n= World")
            .WithFormat(TypstOutputFormat.Svg));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2, result.Outputs.Count);
        var svg = Encoding.UTF8.GetString(result.Outputs[0].ToArray());
        Assert.Contains("<svg", svg);
    }

    [Fact]
    public void Compile_Html_ProducesHtmlDocument()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c
            .WithSource("= Hello HTML\n\nSome paragraph text.")
            .WithFormat(TypstOutputFormat.Html));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Single(result.Outputs);
        var html = Encoding.UTF8.GetString(result.Output.ToArray());
        Assert.Contains("<html", html);
        Assert.Contains("Hello HTML", html);
    }

    [Fact]
    public void Compile_Pdf_DefaultFormat_ProducesSinglePdf()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c
            .WithSource("= Hello")
            .WithFormat(TypstOutputFormat.Pdf));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Single(result.Outputs);
        Assert.Equal(0x25, result.Output.Span[0]); // %
    }
}
