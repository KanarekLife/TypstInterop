namespace TypstInterop.Models;

/// <summary>
/// The output format a Typst document is compiled to.
/// </summary>
public enum TypstOutputFormat : byte
{
    /// <summary>
    /// A single PDF document.
    /// </summary>
    Pdf = 0,

    /// <summary>
    /// One PNG image per page.
    /// </summary>
    Png = 1,

    /// <summary>
    /// One SVG image per page.
    /// </summary>
    Svg = 2,

    /// <summary>
    /// A single HTML document. Note that Typst HTML export is experimental.
    /// </summary>
    Html = 3,
}
