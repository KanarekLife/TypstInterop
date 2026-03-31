using BenchmarkDotNet.Running;

namespace TypstInterop.Benchmarks;

public class Program
{
    public static void Main()
    {
        BenchmarkRunner.Run<TypstBenchmarks>();
    }
}
