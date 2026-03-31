using System.IO;
using TypstInterop;

Console.WriteLine($"Typst Version: {TypstCompiler.TypstVersion}");

const string source = """

    #set page(width: 10cm, height: 5cm)
    #set align(center + horizon)
    #set text(20pt, fill: blue)

    = Hello from .NET 8!

    """;

Console.WriteLine("Compiling...");
using var compiler = new TypstCompiler();
var result = compiler.Compile(c => c.WithSource(source));

if (result is { IsSuccess: true })
{
    const string fileName = "example_net8.pdf";
    File.WriteAllBytes(fileName, result.GetBytes());
    Console.WriteLine($"Successfully compiled to {fileName} ({result.GetBytes().Length} bytes)");
}
else if (!result.IsSuccess)
{
    Console.WriteLine($"Compilation failed: {result.ErrorMessage}");
}
