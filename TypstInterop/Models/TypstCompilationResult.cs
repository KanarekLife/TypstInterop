namespace TypstInterop.Models;

/// <summary>
/// Represents the result of a Typst compilation.
/// </summary>
public readonly struct TypstCompilationResult
{
    private readonly byte[]? _pdfData;

    /// <summary>
    /// Gets a value indicating whether the compilation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the error message if the compilation failed; otherwise, null.
    /// </summary>
    public string? ErrorMessage { get; }

    private TypstCompilationResult(bool success, byte[]? pdfData, string? errorMessage)
    {
        IsSuccess = success;
        _pdfData = pdfData;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a successful compilation result.
    /// </summary>
    /// <param name="data">The PDF data.</param>
    /// <returns>A successful compilation result.</returns>
    internal static TypstCompilationResult Success(byte[] data) => new(true, data, null);

    /// <summary>
    /// Creates a failed compilation result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failed compilation result.</returns>
    internal static TypstCompilationResult Failure(string error) => new(false, null, error);

    /// <summary>
    /// Gets a <see cref="Stream"/> containing the PDF data.
    /// </summary>
    /// <returns>A stream of the PDF data, or <see cref="Stream.Null"/> if compilation failed.</returns>
    public Stream GetStream() => _pdfData != null ? new MemoryStream(_pdfData) : Stream.Null;

    /// <summary>
    /// Gets the raw PDF data as an array.
    /// </summary>
    /// <returns>The PDF data array, or an empty array if compilation failed.</returns>
    public byte[] GetBytes() => _pdfData ?? [];
}
