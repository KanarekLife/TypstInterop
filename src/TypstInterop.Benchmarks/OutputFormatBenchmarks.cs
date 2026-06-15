using BenchmarkDotNet.Attributes;
using TypstInterop.Models;

namespace TypstInterop.Benchmarks;

/// <summary>
/// Output-format benchmarks for TypstInterop (new in v1.1.0).
///
/// Beyond PDF, TypstInterop can render a document to PNG, SVG, and HTML via
/// <see cref="TypstCompileOptions.Format"/>. PDF and HTML produce a single
/// output element; PNG and SVG produce one element per page (exposed as
/// <see cref="TypstCompilationResult.Outputs"/>). These benchmarks characterize
/// the per-format cost on the shared <see cref="Scenarios"/> so the v1.1.0
/// formats are measured the same way as the PDF path.
///
/// These are TypstInterop-only: the competitor (typstsharp) only emits PDF, so
/// the cross-library comparison stays on PDF (see CompetitorBenchmarks.cs).
///
/// All methods reuse a single long-lived compiler (the "Reuse" model — the
/// intended server-side usage); the PDF-vs-fresh / cold-start cost is already
/// characterized in TypstInteropBenchmarks. Methods are tagged with the format
/// (Png/Svg/Html) and the scenario (Simple/Large) categories so the summary
/// groups by format and lines up by scenario. <c>Outputs.Count</c> is summed
/// into the returned value to keep the multi-page output paths from being
/// optimized away.
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
public class OutputFormatBenchmarks
{
    private TypstCompiler _compiler = null!;

    // Reused per-format options (the configure delegate carries the source).
    private static readonly TypstCompileOptions PngOptions =
        new() { Format = TypstOutputFormat.Png, Ppi = 144f };

    private static readonly TypstCompileOptions SvgOptions =
        new() { Format = TypstOutputFormat.Svg };

    private static readonly TypstCompileOptions HtmlOptions =
        new() { Format = TypstOutputFormat.Html };

    [GlobalSetup]
    public void Setup() => _compiler = new TypstCompiler();

    [GlobalCleanup]
    public void Cleanup() => _compiler.Dispose();

    // ---- PNG (one image per page) ----------------------------------------

    [Benchmark]
    [BenchmarkCategory("Png", "Simple")]
    public int Simple_Png() =>
        _compiler.Compile(PngOptions, c => c.WithSource(Scenarios.Simple)).Outputs.Count;

    [Benchmark]
    [BenchmarkCategory("Png", "Large")]
    public int Large_Png() =>
        _compiler.Compile(PngOptions, c => c.WithSource(Scenarios.Large)).Outputs.Count;

    // ---- SVG (one image per page) ----------------------------------------

    [Benchmark]
    [BenchmarkCategory("Svg", "Simple")]
    public int Simple_Svg() =>
        _compiler.Compile(SvgOptions, c => c.WithSource(Scenarios.Simple)).Outputs.Count;

    [Benchmark]
    [BenchmarkCategory("Svg", "Large")]
    public int Large_Svg() =>
        _compiler.Compile(SvgOptions, c => c.WithSource(Scenarios.Large)).Outputs.Count;

    // ---- HTML (single document; Typst HTML export is experimental) -------

    [Benchmark]
    [BenchmarkCategory("Html", "Simple")]
    public int Simple_Html() =>
        _compiler.Compile(HtmlOptions, c => c.WithSource(Scenarios.Simple)).Outputs.Count;

    [Benchmark]
    [BenchmarkCategory("Html", "Large")]
    public int Large_Html() =>
        _compiler.Compile(HtmlOptions, c => c.WithSource(Scenarios.Large)).Outputs.Count;
}
