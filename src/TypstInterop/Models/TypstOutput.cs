using System;
using System.IO;

namespace TypstInterop.Models;

/// <summary>
/// A single artifact produced by a compilation: the PDF or HTML document, or one
/// rendered PNG/SVG page. Exposes the bytes as a span, an owned array, or a stream.
/// </summary>
public readonly struct TypstOutput
{
    private readonly byte[]? _data;

    internal TypstOutput(byte[] data) => _data = data;

    /// <summary>
    /// Gets the number of bytes in this output.
    /// </summary>
    public int Length => _data?.Length ?? 0;

    /// <summary>
    /// Gets a read-only view over the bytes without copying.
    /// </summary>
    public ReadOnlySpan<byte> Span => _data ?? [];

    /// <summary>
    /// Copies the bytes into a new array.
    /// </summary>
    public byte[] ToArray()
    {
        if (_data == null || _data.Length == 0)
            return [];
        var copy = new byte[_data.Length];
        Array.Copy(_data, copy, _data.Length);
        return copy;
    }

    /// <summary>
    /// Creates a new read-only <see cref="Stream"/> over the bytes.
    /// </summary>
    public Stream ToStream() => new MemoryStream(_data ?? [], writable: false);
}
