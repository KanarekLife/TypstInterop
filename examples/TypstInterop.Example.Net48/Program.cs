using System;
using System.IO;
using TypstInterop;

namespace TypstInterop.Example.Net48;

internal class Program
{
    static void Main()
    {
        Console.WriteLine($"Typst Version: {TypstCompiler.TypstVersion}");

        const string source = """

            #set page(width: 10cm, height: 5cm)
            #set align(center + horizon)
            #set text(20pt, fill: blue)

            = Hello from .NET 4.8!

            """;

        Console.WriteLine("Compiling...");
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c.WithSource(source));

        if (result.IsSuccess)
        {
            const string fileName = "example_net48.pdf";
            File.WriteAllBytes(fileName, result.GetBytes());
            Console.WriteLine(
                $"Successfully compiled to {fileName} ({result.GetBytes().Length} bytes)"
            );
        }
        else
        {
            Console.WriteLine($"Compilation failed: {result.ErrorMessage}");
        }
    }
}
