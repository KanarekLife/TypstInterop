using BenchmarkDotNet.Attributes;

namespace TypstInterop.Benchmarks;

[MemoryDiagnoser]
public class TypstBenchmarks
{
    private string _simpleSource = null!;
    private string _complexSource = null!;
    private byte[] _imageData = null!;
    private TypstCompiler _compiler = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compiler = new TypstCompiler();
        _simpleSource = "= Hello World";
        _complexSource = """

                        #import sys: inputs
                        #set page(width: 80mm, height: auto, margin: 5mm)
                        #align(center)[
                            #image("logo.png", width: 20mm)
                            = Receipt
                        ]
                        Customer: #sys.inputs.name
                        Total: #sys.inputs.total
                        
            """;

        // 1x1 PNG
        _imageData = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
        );
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _compiler.Dispose();
    }

    [Benchmark]
    public byte[] BenchmarkTypstInterop()
    {
        var result = _compiler.Compile(c => c.WithSource(_simpleSource));
        return result.GetBytes();
    }

    [Benchmark]
    public byte[] CompileComplex()
    {
        var result = _compiler.Compile(c => c
            .WithFile("logo.png", _imageData)
            .WithInput("name", "Benchmark User")
            .WithInput("total", "$1,000.00")
            .WithSource(_complexSource));
        return result.GetBytes();
    }
}
