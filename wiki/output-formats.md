# Output Formats

By default `Compile` produces a single PDF. Fluent option methods on the builder let you choose a different output format and control export-specific settings such as raster resolution, PDF standard, and document metadata.

```csharp
using TypstInterop;
using TypstInterop.Models;

var result = compiler.Compile(c => c
    .WithSource("= Hello, world!")
    .WithFormat(TypstOutputFormat.Png));
```

Without any `.WithFormat(...)` call the builder defaults to a single PDF export.

## TypstOutputFormat

| Value | Output | Result shape |
| --- | --- | --- |
| `Pdf` (default) | A single PDF document | One element in `Outputs` |
| `Png` | One PNG image **per page** | One element in `Outputs` per page |
| `Svg` | One SVG image **per page** | One element in `Outputs` per page |
| `Html` | A single HTML document (experimental in Typst) | One element in `Outputs` |

## Output option methods

Output settings are configured with fluent methods on the builder, each returning the builder so they can be chained.

| Method | Type | Default | Applies to | Description |
| --- | --- | --- | --- | --- |
| `WithFormat(format)` | `TypstOutputFormat` | `Pdf` | all | The output format. |
| `WithPpi(ppi)` | `float` | `144` | PNG | Resolution in pixels per inch when rasterizing. |
| `WithPdfStandard(standard)` | `TypstPdfStandard` | `Default` | PDF | The PDF standard to conform to. |
| `WithCreationTimestamp(timestamp)` | `DateTimeOffset?` | `null` | PDF | A fixed creation timestamp for reproducible output. |
| `WithPrettyOutput(pretty = true)` | `bool` | `false` | SVG, HTML | Pretty-print the output. |
| `WithTitle(title)` | `string?` | `null` | PDF | Document title metadata. |
| `WithAuthor(author)` | `string?` | `null` | PDF | Document author metadata. |

## Reading the outputs

Every compilation result exposes its produced data through `Outputs` (an `IReadOnlyList<TypstOutput>`). PDF and HTML compilations yield exactly one element; PNG and SVG yield one element per page, in page order.

For single-output formats you can use the convenience accessor:

| Member | Description |
| --- | --- |
| `Output` | The single (or first) produced artifact as a `TypstOutput`. Throws `InvalidOperationException` if there is no output (e.g. on failure). |
| `Outputs` | All produced artifacts as `IReadOnlyList<TypstOutput>` — one for PDF/HTML, one per page for PNG/SVG. |

Each `TypstOutput` exposes `ToArray()` (`byte[]`), `ToStream()` (`Stream`), `Span` (`ReadOnlySpan<byte>`), and `Length`.

### Multi-page PNG / SVG

```csharp
var result = compiler.Compile(c => c
    .WithSource(source)
    .WithFormat(TypstOutputFormat.Png)
    .WithPpi(300));

if (result.IsSuccess)
{
    for (var page = 0; page < result.Outputs.Count; page++)
        File.WriteAllBytes($"page-{page + 1}.png", result.Outputs[page].ToArray());
}
```

SVG export is identical apart from the format and file extension:

```csharp
var result = compiler.Compile(c => c
    .WithSource(source)
    .WithFormat(TypstOutputFormat.Svg)
    .WithPrettyOutput());

for (var page = 0; page < result.Outputs.Count; page++)
    File.WriteAllBytes($"page-{page + 1}.svg", result.Outputs[page].ToArray());
```

### HTML

HTML export produces a single document. Note that Typst's HTML export is experimental.

```csharp
var result = compiler.Compile(c => c
    .WithSource("= Hello, web!")
    .WithFormat(TypstOutputFormat.Html)
    .WithPrettyOutput());

if (result.IsSuccess)
    File.WriteAllText("out.html", System.Text.Encoding.UTF8.GetString(result.Output.ToArray()));
```

## PDF options

### Document metadata

`WithTitle` and `WithAuthor` write into the PDF metadata. They are ignored for non-PDF formats.

```csharp
var result = compiler.Compile(c => c
    .WithSource(source)
    .WithTitle("Quarterly Report")
    .WithAuthor("Acme Corp"));
```

### Reproducible builds with a fixed timestamp

By default the creation timestamp embedded in the PDF comes from the document's own date. Calling `WithCreationTimestamp` pins it to a fixed value, which makes byte-for-byte reproducible PDFs possible.

```csharp
var result = compiler.Compile(c => c
    .WithSource(source)
    .WithCreationTimestamp(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)));
```

### PDF/A and other standards

`WithPdfStandard` requests conformance to a specific PDF standard. This is most relevant for archival (PDF/A) and accessibility (PDF/UA) workflows.

| Value | Standard |
| --- | --- |
| `Default` | PDF 1.7 (default) |
| `V1_7` | PDF 1.7 |
| `V2_0` | PDF 2.0 |
| `A1b` | PDF/A-1b (archival) |
| `A2b` | PDF/A-2b (archival) |
| `A2u` | PDF/A-2u (archival, Unicode) |
| `A3b` | PDF/A-3b (archival, allows embedded files) |
| `A3u` | PDF/A-3u (archival, Unicode, allows embedded files) |
| `Ua1` | PDF/UA-1 (accessibility) |

```csharp
var result = compiler.Compile(c => c
    .WithSource(source)
    .WithPdfStandard(TypstPdfStandard.A2b)
    .WithTitle("Archived Document")
    .WithAuthor("Records Department")
    .WithCreationTimestamp(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)));

if (!result.IsSuccess)
    Console.WriteLine(result.ErrorMessage);
```

> **PDF/A requires a document date.** Archival standards mandate a creation date in the metadata. Either call `WithCreationTimestamp` (recommended for reproducible output) or give the document a date in the source, e.g. `#set document(date: datetime(year: 2024, month: 1, day: 1))`. Without a date the compilation reports an error in `result.Errors` — see [Diagnostics & Error Handling](diagnostics.md).

## See also

- [Diagnostics & Error Handling](diagnostics.md) — inspecting `IsSuccess`, `Errors`, and `Warnings`.
- [Examples](examples.md) — runnable snippets for each configurator method.
