using System;
using System.IO;
using System.Linq;
using TypstInterop.Models;
using Xunit;

namespace TypstInterop.Tests.Integration;

public class PackageDownloadTests
{
    [Fact]
    public void Compile_WithExternalPackage_DownloadsAndSucceeds()
    {
        // Arrange
        // Use a unique temporary directory for the package cache to ensure we are actually downloading
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "TypstInteropTests",
            Guid.NewGuid().ToString()
        );
        var cachePath = Path.Combine(tempDir, "cache");
        var dataPath = Path.Combine(tempDir, "data");

        Directory.CreateDirectory(cachePath);
        Directory.CreateDirectory(dataPath);

        try
        {
            using var compiler = new TypstCompiler(new TypstCompilerOptions
            {
                CachePath = cachePath,
                DataPath = dataPath
            });

            // example package is very simple
            const string source = """
                #import "@preview/example:0.1.0": add
                Sum is #add(2, 3)
                """;
            // Act
            var result = compiler.Compile(c =>
            {
                c.WithSource(source.AsSpan());
            });

            // Assert
            Assert.True(result.IsSuccess, $"Compilation failed: {result.ErrorMessage}");
            Assert.NotNull(result.GetBytes());
            Assert.True(result.GetBytes().Length > 0);

            // Verify that something was actually downloaded to the cache
            var cacheHasContent = Directory.EnumerateFileSystemEntries(cachePath).Any();
            Assert.True(
                cacheHasContent,
                "Package cache directory should not be empty after download."
            );
        }
        finally
        {
            // Cleanup
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // ignored
            }
        }
    }
}
