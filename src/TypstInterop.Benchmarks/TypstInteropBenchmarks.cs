using BenchmarkDotNet.Attributes;

namespace TypstInterop.Benchmarks;

/// <summary>
/// Baseline benchmarks for TypstInterop itself.
///
/// All methods are tagged with the "TypstInterop" benchmark category so that
/// the BenchmarkDotNet summary groups them next to the equivalent competitor
/// methods (see <c>CompetitorBenchmarks.cs</c>), and with a per-scenario
/// category so a scenario can be compared across libraries.
///
/// Reuse model: TypstInterop caches a "world" (fonts + package cache) per
/// <see cref="TypstCompiler"/> instance and resets it between compilations.
/// We therefore measure both:
///   * "Reuse"  - one long-lived compiler reused across the run (the intended
///                server-side usage), and
///   * "Fresh"  - a brand new compiler per compilation (cold-start cost).
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
public class TypstInteropBenchmarks
{
    private TypstCompiler _reusedCompiler = null!;

    [GlobalSetup]
    public void Setup() => _reusedCompiler = new TypstCompiler();

    [GlobalCleanup]
    public void Cleanup() => _reusedCompiler.Dispose();

    // ---- Reuse (long-lived compiler) -------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TypstInterop", "Simple")]
    public byte[] Simple_Reuse() =>
        _reusedCompiler.Compile(c => c.WithSource(Scenarios.Simple)).GetBytes();

    [Benchmark]
    [BenchmarkCategory("TypstInterop", "Inputs")]
    public byte[] Inputs_Reuse() =>
        _reusedCompiler.Compile(c => c
            .WithFile("logo.png", Scenarios.LogoPng)
            .WithInput("name", "Benchmark User")
            .WithInput("total", "$1,000.00")
            .WithSource(Scenarios.WithInputs)).GetBytes();

    [Benchmark]
    [BenchmarkCategory("TypstInterop", "Large")]
    public byte[] Large_Reuse() =>
        _reusedCompiler.Compile(c => c.WithSource(Scenarios.Large)).GetBytes();

    // ---- Fresh (new compiler per compilation; cold start) ----------------

    [Benchmark]
    [BenchmarkCategory("TypstInterop", "Simple")]
    public byte[] Simple_Fresh()
    {
        using var compiler = new TypstCompiler();
        return compiler.Compile(c => c.WithSource(Scenarios.Simple)).GetBytes();
    }

    [Benchmark]
    [BenchmarkCategory("TypstInterop", "Large")]
    public byte[] Large_Fresh()
    {
        using var compiler = new TypstCompiler();
        return compiler.Compile(c => c.WithSource(Scenarios.Large)).GetBytes();
    }
}
