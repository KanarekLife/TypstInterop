using System;
using System.Collections.Generic;
using System.Linq;

namespace TypstInterop.Models;

/// <summary>
/// Represents the result of a Typst compilation.
/// </summary>
public readonly struct TypstCompilationResult
{
    private readonly IReadOnlyList<TypstOutput>? _outputs;
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
    public IReadOnlyList<TypstOutput> Outputs => _outputs ?? [];

    /// <summary>
    /// Gets the single (or first) produced output — the PDF/HTML document or the
    /// first rendered page.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The compilation produced no output (for example, it failed). Check
    /// <see cref="IsSuccess"/> and <see cref="Diagnostics"/> first.
    /// </exception>
    public TypstOutput Output =>
        Outputs.Count > 0
            ? Outputs[0]
            : throw new InvalidOperationException(
                "The compilation produced no output. Inspect IsSuccess and Diagnostics.");

    /// <summary>
    /// Gets the concatenated error messages if the compilation failed; otherwise, null.
    /// </summary>
    public string? ErrorMessage =>
        IsSuccess
            ? null
            : string.Join("\n", Errors.Select(e => e.ToString()));

    private TypstCompilationResult(
        bool success,
        IReadOnlyList<TypstOutput>? outputs,
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
        IReadOnlyList<TypstOutput> outputs,
        IReadOnlyList<TypstDiagnostic> diagnostics) => new(true, outputs, diagnostics);

    /// <summary>
    /// Creates a failed compilation result.
    /// </summary>
    internal static TypstCompilationResult Failure(IReadOnlyList<TypstDiagnostic> diagnostics) =>
        new(false, null, diagnostics);
}
