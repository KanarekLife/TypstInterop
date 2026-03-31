using System;
using System.IO;
using System.Text;

namespace TypstInterop.Abstractions;

/// <summary>
/// Interface for configuring a single Typst compilation job.
/// </summary>
public interface ITypstConfigurator
{
    /// <summary>
    /// Sets the content for the main Typst source file.
    /// </summary>
    ITypstConfigurator WithSource(ReadOnlySpan<char> content);

    /// <summary>
    /// Adds a data or source file (e.g. image, JSON, or additional .typ file).
    /// </summary>
    ITypstConfigurator WithFile(ReadOnlySpan<char> path, ReadOnlySpan<byte> data);

    /// <summary>
    /// Adds a data or source file (e.g. image, JSON, or additional .typ file).
    /// </summary>
    ITypstConfigurator WithFile(ReadOnlySpan<char> path, Stream stream);

    /// <summary>
    /// Adds a text file (e.g. JSON, Typst module).
    /// </summary>
    ITypstConfigurator WithFile(
        ReadOnlySpan<char> path,
        ReadOnlySpan<char> text,
        Encoding? encoding = null
    );

    /// <summary>
    /// Sets a string input (available via #sys.inputs).
    /// </summary>
    ITypstConfigurator WithInput(ReadOnlySpan<char> key, ReadOnlySpan<char> value);

    /// <summary>
    /// Adds a font to the world context for this compilation job.
    /// </summary>
    ITypstConfigurator WithFont(ReadOnlySpan<byte> data);

    /// <summary>
    /// Adds a font to the world context for this compilation job.
    /// </summary>
    ITypstConfigurator WithFont(Stream stream);

    /// <summary>
    /// Mocks a package in the virtual file system.
    /// </summary>
    /// <param name="packageSpec">The package specification (e.g., "@preview/example:1.0.0").</param>
    /// <param name="configure">Configuration for the mocked package.</param>
    ITypstConfigurator WithPackage(string packageSpec, Action<ITypstPackageConfigurator> configure);
}
