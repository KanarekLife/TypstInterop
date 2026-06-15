// =============================================================================
// Competitor benchmarks.
//
// This file is GUARDED by the HAS_TYPSTSHARP compilation symbol (defined in the
// csproj only for net10.0). If the competitor package cannot be restored/built
// for a given TFM/platform, define-out the symbol and the whole suite still
// builds and runs (just without the competitor rows).
//
// -----------------------------------------------------------------------------
// HOW TO ADD A NEW COMPETITOR
// -----------------------------------------------------------------------------
// 1. Add a guarded <PackageReference> in TypstInterop.Benchmarks.csproj, e.g.:
//        <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
//          <PackageReference Include="Some.Typst.Wrapper" Version="x.y.z" />
//        </ItemGroup>
//    and define a matching compilation symbol (e.g. HAS_SOMEWRAPPER) in the
//    same condition's <DefineConstants>.
// 2. Add a new class below wrapped in `#if HAS_SOMEWRAPPER ... #endif`.
// 3. Tag each method with [BenchmarkCategory("<LibName>", "<Scenario>")] using
//    the SAME scenario names as TypstInteropBenchmarks (Simple/Inputs/Large) so
//    the summary lines up. Reuse the markup from `Scenarios`.
// 4. Only implement the scenarios the competitor can actually support; document
//    any omissions in wiki/Benchmarks.md.
// =============================================================================

#if HAS_TYPSTSHARP
using BenchmarkDotNet.Attributes;

// Alias the competitor type to avoid colliding with TypstInterop.TypstCompiler,
// which is in scope via this project's namespace.
using CompetitorCompiler = typstsharp.TypstCompiler;

namespace TypstInterop.Benchmarks;

/// <summary>
/// Benchmarks for the competing <c>typstsharp</c> NuGet package
/// (https://github.com/evolvedlight/typstsharp).
///
/// API/model differences that matter for a fair comparison:
///   * <c>typstsharp</c> takes the Typst source via the static factory
///     <see cref="CompetitorCompiler.FromSource"/>, i.e. the source is bound to the
///     compiler instance. There is no "reuse one compiler across many different
///     documents + reset" mode equivalent to TypstInterop's world reuse, so
///     each compilation creates a new instance. Compare it against
///     TypstInterop's "Fresh" methods.
///   * It has no in-memory virtual file system; external files (e.g. images)
///     must live on disk under a <c>root</c> directory. The Inputs scenario
///     therefore stages <c>logo.png</c> on disk once in [GlobalSetup] (outside
///     the measured region) and points the compiler at that root.
///
/// NOTE: the published 0.14.2.2 package only ships a win-x64 native binary, so
/// these benchmarks only execute on Windows x64. On other platforms the suite
/// still builds; these methods will throw at runtime when the native library
/// cannot be loaded (BenchmarkDotNet reports them as failed without aborting
/// the rest of the run).
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
public class TypstSharpBenchmarks
{
    private string _rootDir = null!;

    [GlobalSetup]
    public void Setup()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "typstsharp-bench-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
        File.WriteAllBytes(Path.Combine(_rootDir, "logo.png"), Scenarios.LogoPng);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_rootDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Benchmark]
    [BenchmarkCategory("typstsharp", "Simple")]
    public byte[] Simple_TypstSharp()
    {
        using var compiler = CompetitorCompiler.FromSource(Scenarios.Simple);
        return compiler.Compile().Buffers[0];
    }

    [Benchmark]
    [BenchmarkCategory("typstsharp", "Inputs")]
    public byte[] Inputs_TypstSharp()
    {
        var sysInputs = new Dictionary<string, string>
        {
            ["name"] = "Benchmark User",
            ["total"] = "$1,000.00",
        };
        using var compiler = CompetitorCompiler.FromSource(
            Scenarios.WithInputs,
            sysInputs: sysInputs,
            root: _rootDir);
        return compiler.Compile().Buffers[0];
    }

    [Benchmark]
    [BenchmarkCategory("typstsharp", "Large")]
    public byte[] Large_TypstSharp()
    {
        using var compiler = CompetitorCompiler.FromSource(Scenarios.Large);
        return compiler.Compile().Buffers[0];
    }
}
#endif
