using System;
using System.IO;
using TypstInterop.Abstractions;
using Xunit;

namespace TypstInterop.Tests.Features;

public class MockingTests
{
    [Fact]
    public void Compile_WithMockedPackage_Succeeds()
    {
        using var compiler = new TypstCompiler();

        var result = compiler.Compile(c =>
        {
            c.WithPackage("@preview/mock:0.1.0", p => p
                .WithSource("lib.typ", "#let x = [Mocked Value]")
                .WithFile("typst.toml", """
                                [package]
                                name = "mock"
                                version = "0.1.0"
                                entrypoint = "lib.typ"
                                """u8)
            );

            c.WithSource(
                """
                                #import "@preview/mock:0.1.0": x
                                Mock content: #x
                """
            );
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.GetBytes());
    }

    [Fact]
    public void Compile_WithMockedPackageFromDirectory_Succeeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "TypstMockPkg_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "lib.typ"), "#let x = [Dir Mocked Value]");
        File.WriteAllText(Path.Combine(tempDir, "typst.toml"), """
            [package]
            name = "dirmock"
            version = "0.1.0"
            entrypoint = "lib.typ"
            """);

        try
        {
            using var compiler = new TypstCompiler();
            var result = compiler.Compile(c =>
            {
                c.WithPackage("@preview/dirmock:0.1.0", p => p.WithDirectory(tempDir));
                c.WithSource("#import \"@preview/dirmock:0.1.0\": x\n#x");
            });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotEmpty(result.GetBytes());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
