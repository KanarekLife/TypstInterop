# Output Formats

By default `Compile` produces a single PDF. The `Compile(TypstCompileOptions, configure)` overload lets you choose a different output format and control export-specific settings such as raster resolution, PDF standard, and document metadata.

```csharp
using TypstInterop;
using TypstInterop.Models;

var result = compiler.Compile(
    new TypstCompileOptions { Format = TypstOutputFormat.Png },
    c => c.WithSource("= Hello, world!"));
```

The single-argument `Compile(configure)` overload is equivalent to passing `new TypstCompileOptions()`, i.e. a default PDF export.

## TypstOutputFormat

| Value | Output | Result shape |
| --- | --- | --- |
| `Pdf` (default) | A single PDF document | One element in `Outputs` |
| `Png` | One PNG image **per page** | One element in `Outputs` per page |
| `Svg` | One SVG image **per page** | One element in `Outputs` per page |
| `Html` | A single HTML document (experimental in Typst) | One element in `Outputs` |

## TypstCompileOptions

| Property | Type | Default | Applies to | Description |
| --- | --- | --- | --- | --- |
| `Format` | `TypstOutputFormat` | `Pdf` | all | The output format. |
| `Ppi` | `float` | `144` | PNG | Resolution in pixels per inch when rasterizing. |
| `PdfStandard` | `TypstPdfStandard` | `Default` | PDF | The PDF standard to conform to. |
| `CreationTimestamp` | `DateTimeOffset?` | `null` | PDF | A fixed creation timestamp for reproducible output. |
| `Pretty` | `bool` | `false` | SVG, HTML | Pretty-print the output. |
| `Title` | `string?` | `null` | PDF | Document title metadata. |
| `Author` | `string?` | `null` | PDF | Document author metadata. |

## Reading the outputs

Every compilation result exposes its produced data through `Outputs` (an `IReadOnlyList<byte[]>`). PDF and HTML compilations yield exactly one element; PNG and SVG yield one element per page, in page order.

For single-output formats you can use the convenience accessors:

| Member | Description |
| --- | --- |
| `Outputs` | All produced outputs, one `byte[]` per element. |
| `GetBytes()` | The first output as a `byte[]` (empty array if none). |
| `GetStream()` | The first output as a `Stream` (`Stream.Null` if none). |

### Multi-page PNG / SVG

```csharp
var result = compiler.Compile(
    new TypstCompileOptions { Format = TypstOutputFormat.Png, Ppi = 300 },
    c => c.WithSource(source));

if (result.IsSuccess)
{
    for (var page = 0; page < result.Outputs.Count; page++)
        File.WriteAllBytes($"page-{page + 1}.png", result.Outputs[page]);
}
```

SVG export is identical apart from the format and file extension:

```csharp
var result = compiler.Compile(
    new TypstCompileOptions { Format = TypstOutputFormat.Svg, Pretty = true },
    c => c.WithSource(source));

for (var page = 0; page < result.Outputs.Count; page++)
    File.WriteAllBytes($"page-{page + 1}.svg", result.Outputs[page]);
```

### HTML

HTML export produces a single document. Note that Typst's HTML export is experimental.

```csharp
var result = compiler.Compile(
    new TypstCompileOptions { Format = TypstOutputFormat.Html, Pretty = true },
    c => c.WithSource("= Hello, web!"));

if (result.IsSuccess)
    File.WriteAllText("out.html", System.Text.Encoding.UTF8.GetString(result.GetBytes()));
```

## PDF options

### Document metadata

`Title` and `Author` are written into the PDF metadata. They are ignored for non-PDF formats.

```csharp
var result = compiler.Compile(
    new TypstCompileOptions
    {
        Title  = "Quarterly Report",
        Author = "Acme Corp"
    },
    c => c.WithSource(source));
```

### Reproducible builds with a fixed timestamp

By default the creation timestamp embedded in the PDF comes from the document's own date. Setting `CreationTimestamp` pins it to a fixed value, which makes byte-for-byte reproducible PDFs possible.

```csharp
var result = compiler.Compile(
    new TypstCompileOptions
    {
        CreationTimestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
    },
    c => c.WithSource(source));
```

### PDF/A and other standards

`PdfStandard` requests conformance to a specific PDF standard. This is most relevant for archival (PDF/A) and accessibility (PDF/UA) workflows.

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
var result = compiler.Compile(
    new TypstCompileOptions
    {
        PdfStandard       = TypstPdfStandard.A2b,
        Title             = "Archived Document",
        Author            = "Records Department",
        CreationTimestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
    },
    c => c.WithSource(source));

if (!result.IsSuccess)
    Console.WriteLine(result.ErrorMessage);
```

> **PDF/A requires a document date.** Archival standards mandate a creation date in the metadata. Either set a `CreationTimestamp` (recommended for reproducible output) or give the document a date in the source, e.g. `#set document(date: datetime(year: 2024, month: 1, day: 1))`. Without a date the compilation reports an error in `result.Errors` — see [Diagnostics & Error Handling](diagnostics.md).

## See also

- [Diagnostics & Error Handling](diagnostics.md) — inspecting `IsSuccess`, `Errors`, and `Warnings`.
- [Examples](examples.md) — runnable snippets for each configurator method.
