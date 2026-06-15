using System;
using TypstInterop.Abstractions;
using Xunit;

namespace TypstInterop.Tests.Features;

public class InputTests
{
    [Fact]
    public void Compile_WithInputs_Succeeds()
    {
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c
            .WithInput("customer", "John Doe")
            .WithInput("amount", "$123.45")
            .WithSource(
                """
                            #import sys: inputs
                            Receipt for: #inputs.customer
                            Total: #inputs.amount
                """
            ));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.GetBytes());
    }
}
