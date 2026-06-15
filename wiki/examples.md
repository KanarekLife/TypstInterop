# Examples

All snippets assume a compiler instance:

```csharp
using System.IO;
using TypstInterop;
using TypstInterop.Models;

using var compiler = new TypstCompiler();
```

The configurator passed to `Compile` exposes the following methods, each returning the configurator so calls can be chained:

| Method | Purpose |
| --- | --- |
| `WithSource(content)` | Sets the main `main.typ` source. |
| `WithFile(path, byte[]/ReadOnlySpan<byte>)` | Adds a binary asset (image, data file). |
| `WithFile(path, Stream)` | Adds a binary asset from a stream. |
| `WithFile(path, text, encoding?)` | Adds a text file or additional `.typ` module. |
| `WithInput(key, value)` | Sets a value readable via `#sys.inputs`. |
| `WithRoot(rootPath)` | Sets (or clears, with `null`) the on-disk project root for this job. |
| `WithFont(byte[]/ReadOnlySpan<byte>)` | Registers an extra font for this job. |
| `WithFont(Stream)` | Registers an extra font from a stream. |
| `WithPackage(spec, configure)` | Mocks a package (`@preview/...`) in the virtual file system. |

## Minimal compile

```csharp
var result = compiler.Compile(c => c.WithSource("= Hello, world!"));

if (result.IsSuccess)
    File.WriteAllBytes("out.pdf", result.GetBytes());
```

## Passing inputs via #sys.inputs

Values set with `WithInput` are exposed inside the document through Typst's `#sys.inputs` dictionary. This is the idiomatic way to inject runtime data without string-building the source.

```csharp
var result = compiler.Compile(c => c
    .WithSource("""
        = #sys.inputs.title
        Prepared for #sys.inputs.client.
        """)
    .WithInput("title", "Quarterly Report")
    .WithInput("client", "Acme Corp"));
```

## Multiple source files

Add additional `.typ` modules with `WithFile` (the text overload) and `#import` them from the main source. A path ending in `.typ` is treated as a Typst source module.

```csharp
var result = compiler.Compile(c => c
    .WithSource("""
        #import "header.typ": project_title
        = #project_title
        Body text goes here.
        """)
    .WithFile("header.typ", "#let project_title = [Automated Report]"));
```

You can nest paths to mirror a directory layout, e.g. `WithFile("sections/intro.typ", "...")` and `#import "sections/intro.typ": ...`.

## Assets like images and data files

Binary assets (images, JSON, CSV, ...) are added with the byte or stream overloads of `WithFile`, then referenced by path from the source.

```csharp
var result = compiler.Compile(c => c
    .WithSource("""
        #image("logo.png", width: 40%)

        #let data = json("data.json")
        Total: #data.total
        """)
    .WithFile("logo.png", File.ReadAllBytes("assets/logo.png"))
    .WithFile("data.json", "{ \"total\": 42 }", System.Text.Encoding.UTF8));
```

Using a stream instead of a byte array:

```csharp
using var imageStream = File.OpenRead("assets/logo.png");

var result = compiler.Compile(c => c
    .WithSource("#image(\"logo.png\")")
    .WithFile("logo.png", imageStream));
```

## Custom fonts

Register a font for the duration of a job with `WithFont`, then select it by family name in the source.

```csharp
var result = compiler.Compile(c => c
    .WithSource("""
        #set text(font: "My Font")
        Hello in a custom typeface.
        """)
    .WithFont(File.ReadAllBytes("fonts/MyFont.ttf")));
```

If you only want the fonts you provide to be considered, construct the compiler with `FontsSource = TypstFontsSource.ProvidedOnly` (see [Configuration](configuration.md#font-source-modes)).

## Mocking a package

`WithPackage` injects a package into the virtual file system so `#import "@preview/...";` resolves without any network access. The configure callback exposes:

| Method | Purpose |
| --- | --- |
| `WithSource(path, text)` | Adds a `.typ` source file to the package. |
| `WithFile(path, byte[]/Stream)` | Adds a data file to the package. |
| `WithDirectory(path, recursive = true)` | Adds every file from a local directory to the package. |

```csharp
var result = compiler.Compile(c => c
    .WithSource("""
        #import "@preview/example:1.0.0": greet
        #greet("World")
        """)
    .WithPackage("@preview/example:1.0.0", pkg => pkg
        .WithSource("lib.typ", "#let greet(name) = [Hello, #name!]")));
```

Loading a package from an on-disk directory:

```csharp
var result = compiler.Compile(c => c
    .WithSource("#import \"@preview/mylib:0.1.0\": *")
    .WithPackage("@preview/mylib:0.1.0", pkg => pkg
        .WithDirectory("./packages/mylib")));
```

An invalid package specification throws `ArgumentException`.

## Exporting PNG, SVG, or HTML

Use the `Compile(TypstCompileOptions, configure)` overload to pick a format. PNG and SVG yield one output per page in `Outputs`.

```csharp
var result = compiler.Compile(
    new TypstCompileOptions { Format = TypstOutputFormat.Png, Ppi = 300 },
    c => c.WithSource("= Page one\n#pagebreak()\n= Page two"));

for (var page = 0; page < result.Outputs.Count; page++)
    File.WriteAllBytes($"page-{page + 1}.png", result.Outputs[page]);
```

See [Output Formats](output-formats.md) for SVG/HTML and the PDF/A and metadata options.

## Compiling an on-disk project

Point the job at a real directory with `WithRoot` so imports and assets are read from disk.

```csharp
var result = compiler.Compile(c => c
    .WithRoot("/path/to/my-typst-project")
    .WithSource(File.ReadAllText("/path/to/my-typst-project/main.typ")));
```

The root can also be set once per compiler via `TypstCompilerOptions.RootPath`. See [Project Root & Fonts](project-root-and-fonts.md).

## Listing available fonts

```csharp
foreach (var family in compiler.ListFonts())
    Console.WriteLine(family);
```

## Inspecting diagnostics

A result carries structured errors and warnings even when it succeeds.

```csharp
var result = compiler.Compile(c => c.WithSource(source));

foreach (var warning in result.Warnings)
    Console.WriteLine($"warning: {warning}");

if (!result.IsSuccess)
    foreach (var error in result.Errors)
        Console.WriteLine($"{error.FilePath}:{error.Line}:{error.Column} {error.Message}");
```

See [Diagnostics & Error Handling](diagnostics.md) for the full `TypstDiagnostic` shape.

## Combining everything

A realistic report job mixing inputs, a module, an image, and a custom font:

```csharp
var result = compiler.Compile(c => c
    .WithSource("""
        #import "header.typ": project_title
        #set text(font: "My Font")
        = #project_title
        Hello #sys.inputs.user!
        #image("logo.png", width: 20%)
        """)
    .WithFile("header.typ", "#let project_title = [Automated Report]")
    .WithFile("logo.png", File.ReadAllBytes("assets/logo.png"))
    .WithFont(File.ReadAllBytes("fonts/MyFont.ttf"))
    .WithInput("user", "Developer"));

if (result.IsSuccess)
    File.WriteAllBytes("report.pdf", result.GetBytes());
else
    Console.WriteLine($"Compilation failed: {result.ErrorMessage}");
```

See [Configuration](configuration.md) for controlling where packages and fonts are allowed to come from.
