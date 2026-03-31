namespace TypstInterop.Models;

/// <summary>
/// Source of packages for the Typst compiler.
/// </summary>
public enum TypstPackagesSource : byte
{
    /// <summary>
    /// All packages from the official repository and those provided manually.
    /// </summary>
    All = 0,
    /// <summary>
    /// Only packages provided manually.
    /// </summary>
    ProvidedOnly = 1,
    /// <summary>
    /// Only packages from the internet (official repository).
    /// </summary>
    InternetOnly = 2,
    /// <summary>
    /// No packages allowed.
    /// </summary>
    None = 3
}

/// <summary>
/// Source of fonts for the Typst compiler.
/// </summary>
public enum TypstFontsSource : byte
{
    /// <summary>
    /// Use all available fonts (System + Default).
    /// </summary>
    All = 0,
    /// <summary>
    /// Use only default (embedded) fonts.
    /// </summary>
    DefaultOnly = 1,
    /// <summary>
    /// Use only system fonts.
    /// </summary>
    SystemOnly = 2,
    /// <summary>
    /// Use only fonts provided via WithFont.
    /// </summary>
    ProvidedOnly = 3,
    /// <summary>
    /// No fonts allowed.
    /// </summary>
    None = 4
}

/// <summary>
/// Configuration options for the Typst compiler.
/// </summary>
public sealed class TypstCompilerOptions
{
    /// <summary>
    /// Gets or sets the source of packages.
    /// </summary>
    public TypstPackagesSource PackagesSource { get; set; } = TypstPackagesSource.All;

    /// <summary>
    /// Gets or sets the source of fonts.
    /// </summary>
    public TypstFontsSource FontsSource { get; set; } = TypstFontsSource.All;

    /// <summary>
    /// Gets or sets the path to the package cache directory.
    /// If null, the default system cache path will be used.
    /// </summary>
    public string? CachePath { get; set; }

    /// <summary>
    /// Gets or sets the path to the package data directory.
    /// If null, the default system data path will be used.
    /// </summary>
    public string? DataPath { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypstCompilerOptions"/> class with default values.
    /// </summary>
    public TypstCompilerOptions() { }
}
