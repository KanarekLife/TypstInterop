using System;
using System.Collections.Generic;
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
    /// Executes the compilation to PDF and returns the result.
    /// State added via <see cref="ITypstConfigurator"/> is reset before each call.
    /// </summary>
    /// <param name="configure">Configuration action for this specific compilation run.</param>
    TypstCompilationResult Compile(Action<ITypstConfigurator> configure);

    /// <summary>
    /// Executes the compilation using the supplied options (output format,
    /// PDF/PNG/SVG/HTML settings, document metadata) and returns the result.
    /// State added via <see cref="ITypstConfigurator"/> is reset before each call.
    /// </summary>
    /// <param name="options">The per-compilation options.</param>
    /// <param name="configure">Configuration action for this specific compilation run.</param>
    TypstCompilationResult Compile(TypstCompileOptions options, Action<ITypstConfigurator> configure);

    /// <summary>
    /// Lists the font family names currently available to the compiler, taking
    /// the configured font source into account.
    /// </summary>
    IReadOnlyList<string> ListFonts();
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
