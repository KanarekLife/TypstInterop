using System;
using System.IO;
using System.Text;
using TypstInterop.Abstractions;
using Xunit;

namespace TypstInterop.Tests.Features;

public class AssetTests
{
    private static readonly byte[] Black1x1Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
    );

    [Fact]
    public void Compile_WithPng_Succeeds()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c
            .WithFile("image.png", Black1x1Png)
            .WithSource("""#image("image.png")"""));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.GetBytes());
        Assert.True(result.GetBytes().Length > 100, "PDF should have a reasonable size");
    }

    [Fact]
    public void Compile_WithJsonData_Succeeds()
    {
        const string json = """{ "name": "Typst", "version": "0.14.2" }""";

        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c
            .WithFile("data.json", Encoding.UTF8.GetBytes(json))
            .WithSource(
                @"
           #let data = json(""data.json"")
           Hello from #data.name version #data.version
           "
            ));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.GetBytes());
    }

    [Fact]
    public void Compile_WithSvg_Succeeds()
    {
        const string svgData =
            """<svg viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg"><rect width="100" height="100" fill="blue"/></svg>""";

        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c
            .WithFile("image.svg", Encoding.UTF8.GetBytes(svgData))
            .WithSource(@"#image(""image.svg"", width: 2cm)"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.GetBytes());
        Assert.True(result.GetBytes().Length > 100, "PDF should have a reasonable size");
    }
}
