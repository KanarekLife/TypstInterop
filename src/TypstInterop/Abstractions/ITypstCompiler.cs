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
    /// Gets the version of the underlying Typst compiler. Instance accessor for
    /// the static <see cref="TypstInterop.TypstCompiler.TypstVersion"/>.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Executes a compilation and returns the result. The supplied function
    /// receives a fresh <see cref="ITypstConfigurator"/>, configures the source,
    /// assets, and output options fluently, and returns the same builder.
    /// State added via <see cref="ITypstConfigurator"/> is reset before each call.
    /// </summary>
    /// <param name="build">
    /// A fluent configuration function for this specific compilation run. It must
    /// return the configurator it was given (the builder pattern).
    /// </param>
    TypstCompilationResult Compile(Func<ITypstConfigurator, ITypstConfigurator> build);

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
