using System.Linq;
using TypstInterop.Models;
using Xunit;

namespace TypstInterop.Tests.Features;

public class FontListingTests
{
    [Fact]
    public void ListFonts_WithEmbeddedFonts_ReturnsFamilies()
    {
        using var compiler = new TypstCompiler(new TypstCompilerOptions
        {
            FontsSource = TypstFontsSource.DefaultOnly,
        });

        var fonts = compiler.ListFonts();

        Assert.NotEmpty(fonts);
        // Typst ships with the "New Computer Modern" family among its embedded fonts.
        Assert.Contains(fonts, f => f.Contains("New Computer Modern"));
    }

    [Fact]
    public void ListFonts_NoFonts_ReturnsEmpty()
    {
        using var compiler = new TypstCompiler(new TypstCompilerOptions
        {
            FontsSource = TypstFontsSource.None,
        });

        var fonts = compiler.ListFonts();

        Assert.Empty(fonts);
    }
}
