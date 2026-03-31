using System;
using System.IO;
using TypstInterop.Models;

namespace TypstInterop.Abstractions;

/// <summary>
/// Interface for a Typst compiler.
/// Each instance handles a single Typst World and can be reused for multiple compilations.
/// </summary>
public interface ITypstCompiler : IDisposable
{
    /// <summary>
    /// Executes the compilation and returns the PDF result.
    /// State added via <see cref="ITypstConfigurator"/> is reset before each call.
    /// </summary>
    /// <param name="configure">Configuration action for this specific compilation run.</param>
    TypstCompilationResult Compile(Action<ITypstConfigurator> configure);
}

/// <summary>
/// Interface for configuring a mocked package.
/// </summary>
public interface ITypstPackageConfigurator
{
    /// <summary>
    /// Adds a source file to the package.
    /// </summary>
    ITypstPackageConfigurator WithSource(ReadOnlySpan<char> path, ReadOnlySpan<char> text);

    /// <summary>
    /// Adds a data file to the package.
    /// </summary>
    ITypstPackageConfigurator WithFile(ReadOnlySpan<char> path, ReadOnlySpan<byte> data);

    /// <summary>
    /// Adds a data file to the package.
    /// </summary>
    ITypstPackageConfigurator WithFile(ReadOnlySpan<char> path, Stream stream);

    /// <summary>
    /// Adds all files from the specified directory to the package.
    /// </summary>
    /// <param name="directoryPath">The path to the directory.</param>
    /// <param name="recursive">Whether to include files in subdirectories.</param>
    ITypstPackageConfigurator WithDirectory(string directoryPath, bool recursive = true);
}
