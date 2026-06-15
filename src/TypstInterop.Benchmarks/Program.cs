using System.Reflection;
using BenchmarkDotNet.Running;

namespace TypstInterop.Benchmarks;

public class Program
{
    // Runs every [*]Benchmarks class in this assembly (TypstInterop + any
    // guarded competitor classes). Pass standard BenchmarkDotNet args through,
    // e.g. `-- --filter '*Simple*'` or `-- --filter '*'`.
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
}
