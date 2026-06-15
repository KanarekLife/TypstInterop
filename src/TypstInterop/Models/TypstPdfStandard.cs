namespace TypstInterop.Models;

/// <summary>
/// PDF standards that the compiler can be asked to conform to when exporting PDF.
/// </summary>
public enum TypstPdfStandard : byte
{
    /// <summary>
    /// The default standard (PDF 1.7).
    /// </summary>
    Default = 0,

    /// <summary>
    /// PDF/A-2b (archival).
    /// </summary>
    A2b = 1,

    /// <summary>
    /// PDF/A-3b (archival, allows embedded files).
    /// </summary>
    A3b = 2,

    /// <summary>
    /// PDF 1.7.
    /// </summary>
    V1_7 = 3,

    /// <summary>
    /// PDF/A-1b (archival).
    /// </summary>
    A1b = 4,

    /// <summary>
    /// PDF/A-2u (archival, Unicode).
    /// </summary>
    A2u = 5,

    /// <summary>
    /// PDF/A-3u (archival, Unicode, allows embedded files).
    /// </summary>
    A3u = 6,

    /// <summary>
    /// PDF 2.0.
    /// </summary>
    V2_0 = 7,

    /// <summary>
    /// PDF/UA-1 (accessibility).
    /// </summary>
    Ua1 = 8,
}
