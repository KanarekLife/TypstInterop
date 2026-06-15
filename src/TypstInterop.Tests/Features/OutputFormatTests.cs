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
        var result = compiler.Compile(
            new TypstCompileOptions { Format = TypstOutputFormat.Png, Ppi = 96 },
            c => c.WithSource("= Page One\n#pagebreak()\n= Page Two"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2, result.Outputs.Count);
        foreach (var page in result.Outputs)
        {
            Assert.NotEmpty(page);
            // PNG magic number: 0x89 'P' 'N' 'G'
            Assert.Equal(0x89, page[0]);
            Assert.Equal((byte)'P', page[1]);
            Assert.Equal((byte)'N', page[2]);
            Assert.Equal((byte)'G', page[3]);
        }
    }

    [Fact]
    public void Compile_Svg_ProducesSvgPerPage()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(
            new TypstCompileOptions { Format = TypstOutputFormat.Svg },
            c => c.WithSource("= Hello\n#pagebreak()\n= World"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2, result.Outputs.Count);
        var svg = Encoding.UTF8.GetString(result.Outputs[0]);
        Assert.Contains("<svg", svg);
    }

    [Fact]
    public void Compile_Html_ProducesHtmlDocument()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(
            new TypstCompileOptions { Format = TypstOutputFormat.Html },
            c => c.WithSource("= Hello HTML\n\nSome paragraph text."));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Single(result.Outputs);
        var html = Encoding.UTF8.GetString(result.GetBytes());
        Assert.Contains("<html", html);
        Assert.Contains("Hello HTML", html);
    }

    [Fact]
    public void Compile_Pdf_DefaultFormat_ProducesSinglePdf()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(
            new TypstCompileOptions { Format = TypstOutputFormat.Pdf },
            c => c.WithSource("= Hello"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Single(result.Outputs);
        Assert.Equal(0x25, result.GetBytes()[0]); // %
    }
}
