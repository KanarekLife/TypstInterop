# Getting Started

## Installation

Add the package to your project:

```bash
dotnet add package TypstInterop
```

The correct native binary for your OS and architecture ships in a companion `TypstInterop.runtime.<rid>` package, selected through a `runtime.json` RID graph in the main package.

## Set a RuntimeIdentifier

NuGet only restores a `runtime.json` runtime dependency when your project restores **for a specific runtime identifier (RID)**. A plain framework-dependent build (`dotnet build` / `dotnet run` with no RID) therefore never pulls the native, and the app fails at startup with:

```
System.DllNotFoundException: Unable to load shared library 'typst_interop' ...
```

So a consuming **application** must declare the RID(s) it runs on. Set a single RID in the project file:

```xml
<PropertyGroup>
  <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
</PropertyGroup>
```

…or list several and pick one per build/publish:

```xml
<PropertyGroup>
  <RuntimeIdentifiers>win-x64;linux-x64;linux-musl-x64;osx-arm64</RuntimeIdentifiers>
</PropertyGroup>
```

```bash
dotnet run     -r linux-x64
dotnet publish -r linux-x64
```

Supported RIDs: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64` (Alpine), `linux-musl-arm64` (Alpine), `osx-x64`, `osx-arm64`.

> If you build an executable without a RID, the package emits build warning **`TYPST0001`** to catch this before runtime. Class-library projects don't warn — they defer the RID choice to the final app. Suppress with `<TypstInteropSuppressRuntimeIdentifierWarning>true</TypstInteropSuppressRuntimeIdentifierWarning>`, or turn it into a hard error with `<TypstInteropTreatMissingRuntimeIdentifierAsError>true</TypstInteropTreatMissingRuntimeIdentifierAsError>`.

## Your first PDF

The entry point is the `TypstInterop.TypstCompiler` class. It is disposable and owns the native Typst engine life-cycle, so wrap it in a `using` statement. Each `Compile` call takes a configurator action that describes the job.

```csharp
using System.IO;
using TypstInterop;

using var compiler = new TypstCompiler();

const string source = """
    #set page(width: 10cm, height: 5cm)
    #set align(center + horizon)
    #set text(20pt, fill: blue)

    = Hello from .NET!
    """;

var result = compiler.Compile(c => c.WithSource(source));

if (result.IsSuccess)
{
    File.WriteAllBytes("hello.pdf", result.Output.ToArray());
    Console.WriteLine($"Wrote {result.Output.Length} bytes.");
}
else
{
    Console.WriteLine($"Compilation failed: {result.ErrorMessage}");
}
```

## Reading the result

`Compile` returns a `TypstCompilationResult` (in `TypstInterop.Models`):

| Member | Description |
| --- | --- |
| `IsSuccess` | `true` when compilation succeeded. |
| `ErrorMessage` | The error text when `IsSuccess` is `false`; otherwise `null`. |
| `Diagnostics` / `Errors` / `Warnings` | Structured diagnostics (see [Diagnostics & Error Handling](diagnostics.md)). |
| `Output` | The single (or first) produced artifact as a `TypstOutput`. Throws `InvalidOperationException` if there is no output (e.g. on failure). |
| `Outputs` | All produced artifacts as `IReadOnlyList<TypstOutput>` — one for PDF/HTML, one per page for PNG/SVG. |

Each `TypstOutput` exposes `ToArray()` (`byte[]`), `ToStream()` (`Stream`), `Span` (`ReadOnlySpan<byte>`), and `Length`.

Always check `IsSuccess` before using the data:

```csharp
var result = compiler.Compile(c => c.WithSource(source));
if (!result.IsSuccess)
    throw new InvalidOperationException(result.ErrorMessage);

using var stream = result.Output.ToStream();
// copy to a file, HTTP response, blob store, etc.
```

## Checking the embedded Typst version

```csharp
Console.WriteLine(TypstCompiler.TypstVersion); // "0.15.0"
```

The same value is available as an instance property on `ITypstCompiler`, which forwards to `TypstCompiler.TypstVersion`:

```csharp
Console.WriteLine(compiler.Version); // "0.15.0"
```

## Reusing a compiler

A single `TypstCompiler` instance can be reused for many compilations. State added in the configurator (sources, files, inputs, fonts) is reset before each `Compile` call, so jobs do not leak into one another:

```csharp
using var compiler = new TypstCompiler();

var first  = compiler.Compile(c => c.WithSource("First"));
var second = compiler.Compile(c => c.WithSource("Second"));
```

## Other output formats

The builder also exposes fluent output options that let you export PNG, SVG, or HTML and set PDF options (PDF/A standard, title/author metadata, fixed timestamp):

```csharp
var result = compiler.Compile(c => c
    .WithSource(source)
    .WithFormat(TypstOutputFormat.Png));

for (var page = 0; page < result.Outputs.Count; page++)
    File.WriteAllBytes($"page-{page + 1}.png", result.Outputs[page].ToArray());
```

See [Output Formats](output-formats.md) for the full set of formats and options.

## Next steps

- [Configuration](configuration.md) — control package and font resolution, set cache/data paths, run offline.
- [Output Formats](output-formats.md) — PDF, PNG, SVG, and HTML export plus PDF/A and metadata options.
- [Diagnostics & Error Handling](diagnostics.md) — structured errors and warnings.
- [Project Root & Fonts](project-root-and-fonts.md) — compile an on-disk project and list available fonts.
- [Examples](examples.md) — inputs, custom fonts, multiple source files, images, and mocked packages.
