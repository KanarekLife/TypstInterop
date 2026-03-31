using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TypstInterop.Abstractions;
using Xunit;

namespace TypstInterop.Tests.Integration;

public class ParallelTests
{
    [Fact]
    public void ParallelCompilation_MultipleCompilers_Succeeds()
    {
        // Arrange
        int compilerCount = 4;

        // Act
        Parallel.For(
            0,
            compilerCount,
            i =>
            {
                using var compiler = new TypstCompiler();
                var result = compiler.Compile(c => c.WithSource($"= Compiler {i}".AsSpan()));
                Assert.True(result.IsSuccess);
            }
        );
    }

    [Fact]
    public async Task HeavyParallelLoad_Succeeds()
    {
        // Arrange
        int iterations = 50;

        // Act
        var tasks = Enumerable
            .Range(0, iterations)
            .Select(i =>
                Task.Run(() =>
                {
                    string content =
                        $"= Iteration {i}\n"
                        + string.Join("\n", Enumerable.Range(0, 100).Select(j => $"Line {j}"));
                    using var compiler = new TypstCompiler();
                    return compiler.Compile(c => c.WithSource(content.AsSpan()));
                })
            );

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.True(r.IsSuccess));
    }
}
