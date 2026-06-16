using System;

namespace TypstInterop.Models;

/// <summary>
/// Per-compilation options controlling the output format and export settings.
/// Accumulated internally via the <c>With*</c> methods on
/// <see cref="TypstInterop.Abstractions.ITypstConfigurator"/>.
/// </summary>
internal sealed class TypstCompileOptions
{
    /// <summary>
    /// Gets or sets the output format. Defaults to <see cref="TypstOutputFormat.Pdf"/>.
    /// </summary>
    public TypstOutputFormat Format { get; set; } = TypstOutputFormat.Pdf;

    /// <summary>
    /// Gets or sets the resolution in pixels per inch used when rendering to
    /// <see cref="TypstOutputFormat.Png"/>. Defaults to 144. Ignored for other formats.
    /// </summary>
    public float Ppi { get; set; } = 144f;

    /// <summary>
    /// Gets or sets the PDF standard to conform to. Ignored for non-PDF formats.
    /// </summary>
    public TypstPdfStandard PdfStandard { get; set; } = TypstPdfStandard.Default;

    /// <summary>
    /// Gets or sets a fixed creation timestamp to embed in the PDF for
    /// reproducible output. When null, the document's own date is used.
    /// Ignored for non-PDF formats.
    /// </summary>
    public DateTimeOffset? CreationTimestamp { get; set; }

    /// <summary>
    /// Gets or sets whether SVG/HTML output should be pretty-printed.
    /// Ignored for PDF and PNG.
    /// </summary>
    public bool Pretty { get; set; }

    /// <summary>
    /// Gets or sets the document title metadata (PDF only).
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the document author metadata (PDF only).
    /// </summary>
    public string? Author { get; set; }
}
