using System.IO;
using TypstInterop.Models;
using Xunit;

namespace TypstInterop.Tests.Features;

public class RootPathTests
{
    [Fact]
    public void Compile_WithOnDiskRoot_ReadsRealFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "typstinterop-root-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "data.typ"), "#let value = [On-disk content]");

            using var compiler = new TypstCompiler(new TypstCompilerOptions { RootPath = dir });
            var result = compiler.Compile(c => c
                .WithSource("#import \"data.typ\": value\nLoaded: #value"));

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotEmpty(result.Output.ToArray());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Compile_WithRootConfigurator_ReadsRealFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "typstinterop-root-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "snippet.typ"), "#let msg = [hi from disk]");

            using var compiler = new TypstCompiler();
            var result = compiler.Compile(c => c
                .WithRoot(dir)
                .WithSource("#import \"snippet.typ\": msg\n#msg"));

            Assert.True(result.IsSuccess, result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Compile_WithoutRoot_CannotReadDiskFiles()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c
            .WithSource("#import \"does-not-exist.typ\": x\n#x"));

        Assert.False(result.IsSuccess);
    }
}
