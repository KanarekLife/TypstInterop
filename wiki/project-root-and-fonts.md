# On-Disk Project Root & Font Listing

In addition to providing every file in memory, TypstInterop can compile a real Typst project that lives on disk, and it can report which font families are currently available.

## Compiling an on-disk project (project root)

Setting a root directory makes the compiler read project files (imports, images, data) directly from disk relative to that directory, mirroring the Typst CLI's `--root` option. This is the simplest way to compile an existing Typst project without wiring up each file with `WithFile`.

You can set the root in two ways.

### Per compiler instance: `RootPath`

`TypstCompilerOptions.RootPath` sets the root for every compilation done by that compiler.

```csharp
using TypstInterop;
using TypstInterop.Models;

using var compiler = new TypstCompiler(new TypstCompilerOptions
{
    RootPath = "/path/to/my-typst-project"
});

var result = compiler.Compile(c => c
    .WithSource(File.ReadAllText("/path/to/my-typst-project/main.typ")));
```

With the root set, the `main.typ` above can `#import "chapters/intro.typ"` or `#image("assets/logo.png")` and those files are loaded from disk relative to the root.

### Per compilation: `WithRoot`

`WithRoot` sets (or, with `null`, clears) the root for a single compilation job. It overrides the per-instance `RootPath` for that call.

```csharp
var result = compiler.Compile(c => c
    .WithRoot("/path/to/my-typst-project")
    .WithSource(File.ReadAllText("/path/to/my-typst-project/main.typ")));
```

Notes:

- Files supplied in-memory (via `WithSource` / `WithFile`) take precedence; anything not provided in memory is then read from the root directory on disk.
- Passing `null` to `WithRoot` clears the root so only in-memory files are served.
- When no root is configured, only in-memory files are available.

## Listing available fonts

`ListFonts()` returns the font family names the compiler can currently use, honoring the configured [font source](configuration.md#font-source-modes). It is useful for diagnosing missing-font issues and for letting users pick from the fonts that are actually available.

```csharp
using var compiler = new TypstCompiler();

IReadOnlyList<string> fonts = compiler.ListFonts();
foreach (var family in fonts)
    Console.WriteLine(family);
```

The result reflects the compiler's font configuration, so it also includes fonts you add per job. For example, restricting to embedded fonts only:

```csharp
using var compiler = new TypstCompiler(new TypstCompilerOptions
{
    FontsSource = TypstFontsSource.DefaultOnly
});

foreach (var family in compiler.ListFonts())
    Console.WriteLine(family); // only the embedded default fonts
```

## See also

- [Configuration](configuration.md) — `RootPath` and font source modes.
- [Examples](examples.md) — `WithFile`, `WithFont`, and package mocking.
