namespace TypstInterop.Benchmarks;

/// <summary>
/// Single source of truth for the benchmark scenarios. Both the TypstInterop
/// benchmarks and the competitor benchmarks consume the exact same markup and
/// data so that the comparison is apples-to-apples.
///
/// Scenario design goals:
///   * Simple  - the minimal "hello world" document; measures fixed overhead.
///   * Inputs  - a small document that reads sys.inputs and embeds an image;
///               measures the cost of supplying external data/files.
///   * Large   - a multi-page, multi-element document (headings, tables,
///               lists, repeated content); measures throughput on real work.
/// </summary>
public static class Scenarios
{
    /// <summary>Minimal document. Exercises fixed per-compilation overhead.</summary>
    public const string Simple = "= Hello World";

    /// <summary>
    /// Document that consumes sys.inputs and an embedded image.
    /// NOTE: kept self-contained (no system fonts assumed) so it compiles the
    /// same way across libraries.
    /// </summary>
    public const string WithInputs = """
        #import sys: inputs
        #set page(width: 80mm, height: auto, margin: 5mm)
        #align(center)[
            #image("logo.png", width: 20mm)
            = Receipt
        ]
        Customer: #sys.inputs.at("name", default: "N/A")
        Total: #sys.inputs.at("total", default: "0")
        """;

    /// <summary>A 1x1 PNG used by the <see cref="WithInputs"/> scenario.</summary>
    public static readonly byte[] LogoPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
    );

    /// <summary>
    /// A larger, multi-element document: several pages worth of headings,
    /// paragraphs, a table, and a bullet list rendered in a loop.
    /// </summary>
    public static readonly string Large = BuildLargeDocument();

    private static string BuildLargeDocument()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#set page(width: 210mm, height: 297mm, margin: 20mm)");
        sb.AppendLine("#set heading(numbering: \"1.\")");
        sb.AppendLine("= Benchmark Report");
        sb.AppendLine();
        sb.AppendLine("#table(");
        sb.AppendLine("  columns: 3,");
        sb.AppendLine("  [*Item*], [*Qty*], [*Price*],");
        for (var i = 1; i <= 20; i++)
        {
            sb.AppendLine($"  [Item {i}], [{i}], [\\${i * 3}.00],");
        }
        sb.AppendLine(")");
        sb.AppendLine();

        // Several sections with prose and lists to push the document over a
        // single page and exercise layout on repeated content.
        for (var section = 1; section <= 8; section++)
        {
            sb.AppendLine($"== Section {section}");
            sb.AppendLine(
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit, "
                    + "sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. "
                    + "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris.");
            sb.AppendLine();
            for (var bullet = 1; bullet <= 6; bullet++)
            {
                sb.AppendLine($"- Point {bullet} of section {section}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
