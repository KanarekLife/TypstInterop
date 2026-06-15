using System.Collections.Generic;
using System.Linq;

namespace TypstInterop.Models;

/// <summary>
/// Represents the result of a Typst compilation.
/// </summary>
public readonly struct TypstCompilationResult
{
    private readonly IReadOnlyList<byte[]>? _outputs;
    private readonly IReadOnlyList<TypstDiagnostic>? _diagnostics;

    /// <summary>
    /// Gets a value indicating whether the compilation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets all diagnostics (errors and warnings) produced during compilation.
    /// </summary>
    public IReadOnlyList<TypstDiagnostic> Diagnostics => _diagnostics ?? [];

    /// <summary>
    /// Gets only the warning diagnostics.
    /// </summary>
    public IEnumerable<TypstDiagnostic> Warnings =>
        Diagnostics.Where(d => d.Severity == TypstDiagnosticSeverity.Warning);

    /// <summary>
    /// Gets only the error diagnostics.
    /// </summary>
    public IEnumerable<TypstDiagnostic> Errors =>
        Diagnostics.Where(d => d.Severity == TypstDiagnosticSeverity.Error);

    /// <summary>
    /// Gets the produced outputs. A PDF or HTML compilation yields a single
    /// element; PNG and SVG compilations yield one element per page.
    /// </summary>
    public IReadOnlyList<byte[]> Outputs => _outputs ?? [];

    /// <summary>
    /// Gets the concatenated error messages if the compilation failed; otherwise, null.
    /// </summary>
    public string? ErrorMessage =>
        IsSuccess
            ? null
            : string.Join("\n", Errors.Select(e => e.ToString()));

    private TypstCompilationResult(
        bool success,
        IReadOnlyList<byte[]>? outputs,
        IReadOnlyList<TypstDiagnostic>? diagnostics)
    {
        IsSuccess = success;
        _outputs = outputs;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Creates a successful compilation result.
    /// </summary>
    internal static TypstCompilationResult Success(
        IReadOnlyList<byte[]> outputs,
        IReadOnlyList<TypstDiagnostic> diagnostics) => new(true, outputs, diagnostics);

    /// <summary>
    /// Creates a failed compilation result.
    /// </summary>
    internal static TypstCompilationResult Failure(IReadOnlyList<TypstDiagnostic> diagnostics) =>
        new(false, null, diagnostics);

    /// <summary>
    /// Gets a <see cref="Stream"/> containing the first output (e.g. the PDF or
    /// the first rendered page), or <see cref="Stream.Null"/> if there is none.
    /// </summary>
    public Stream GetStream() =>
        Outputs.Count > 0 ? new MemoryStream(Outputs[0]) : Stream.Null;

    /// <summary>
    /// Gets the first output as a byte array (e.g. the PDF or the first rendered
    /// page), or an empty array if there is none.
    /// </summary>
    public byte[] GetBytes() => Outputs.Count > 0 ? Outputs[0] : [];
}
