namespace TypstInterop.Models;

/// <summary>
/// The severity of a Typst diagnostic.
/// </summary>
public enum TypstDiagnosticSeverity : byte
{
    /// <summary>
    /// A fatal error that prevented compilation from producing output.
    /// </summary>
    Error = 0,

    /// <summary>
    /// A non-fatal warning. Output is still produced.
    /// </summary>
    Warning = 1,
}

/// <summary>
/// A structured compilation diagnostic (error or warning), including its
/// source position when available.
/// </summary>
public sealed class TypstDiagnostic
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TypstDiagnostic"/> class.
    /// </summary>
    public TypstDiagnostic(
        TypstDiagnosticSeverity severity,
        string message,
        string? filePath,
        int line,
        int column,
        string? hints)
    {
        Severity = severity;
        Message = message;
        FilePath = filePath;
        Line = line;
        Column = column;
        Hints = hints;
    }

    /// <summary>
    /// Gets the severity of the diagnostic.
    /// </summary>
    public TypstDiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the path of the file the diagnostic points into, or null when the
    /// diagnostic is not associated with a specific file.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Gets the 1-based line number, or 0 when unavailable.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the 1-based column number, or 0 when unavailable.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets additional newline-separated hints, or null when there are none.
    /// </summary>
    public string? Hints { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var location = FilePath is null
            ? string.Empty
            : Line > 0 ? $"{FilePath}:{Line}:{Column}: " : $"{FilePath}: ";
        return $"{location}{Severity.ToString().ToLowerInvariant()}: {Message}";
    }
}
