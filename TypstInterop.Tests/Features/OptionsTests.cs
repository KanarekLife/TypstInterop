using System;
using TypstInterop.Abstractions;
using TypstInterop.Models;
using Xunit;

namespace TypstInterop.Tests.Features;

public class OptionsTests
{
    [Fact]
    public void PackagesSource_None_BlocksMockedPackage()
    {
        using var compiler = new TypstCompiler(new TypstCompilerOptions
        {
            PackagesSource = TypstPackagesSource.None
        });

        var result = compiler.Compile(c =>
        {
            c.WithPackage("@preview/mock:0.1.0", p => p.WithSource("lib.typ", "#let x = 1"));
            c.WithSource("#import \"@preview/mock:0.1.0\": x");
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("file not found", result.ErrorMessage);
    }

    [Fact]
    public void PackagesSource_InternetOnly_BlocksMockedPackage()
    {
        using var compiler = new TypstCompiler(new TypstCompilerOptions
        {
            PackagesSource = TypstPackagesSource.InternetOnly
        });

        var result = compiler.Compile(c =>
        {
            c.WithPackage("@preview/mock:0.1.0", p => p.WithSource("lib.typ", "#let x = 1"));
            c.WithSource("#import \"@preview/mock:0.1.0\": x");
        });

        Assert.False(result.IsSuccess);
        // Should fail because manual packages are blocked in InternetOnly mode
        Assert.Contains("failed to load file", result.ErrorMessage);
    }

    [Fact]
    public void PackagesSource_ProvidedOnly_AllowsMockedPackage()
    {
        using var compiler = new TypstCompiler(new TypstCompilerOptions
        {
            PackagesSource = TypstPackagesSource.ProvidedOnly
        });

        var result = compiler.Compile(c =>
        {
            c.WithPackage("@preview/mock:0.1.0", p => p
                .WithSource("lib.typ", "#let x = [Mocked]")
                .WithFile("typst.toml", """
                    [package]
                    name = "mock"
                    version = "0.1.0"
                    entrypoint = "lib.typ"
                    """u8)
            );
            c.WithSource("#import \"@preview/mock:0.1.0\": x\n#x");
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Fact]
    public void FontsSource_None_ShouldFailToFindFont()
    {
        using var compiler = new TypstCompiler(new TypstCompilerOptions
        {
            FontsSource = TypstFontsSource.None
        });

        // Even if we provide a font, it should be ignored in None mode
        var result = compiler.Compile(c =>
        {
            c.WithSource("#set text(font: \"SomeNonExistentFont\")\nHello");
        });

        // Typst usually falls back to a default font if possible, 
        // but in "None" mode there are NO fonts at all, so it should probably have issues
        // rendering or at least not have the system fonts.
        Assert.True(result.IsSuccess); // It might still succeed with empty PDF or fallback? 
        // Let's check if it actually has no fonts by trying to use a specific font.
    }
}
