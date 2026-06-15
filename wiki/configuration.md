# Configuration

The `TypstCompiler` can be constructed with default options or with a `TypstCompilerOptions` instance (namespace `TypstInterop.Models`). Options are applied per compiler instance, not per `Compile` call.

```csharp
using TypstInterop;
using TypstInterop.Models;

using var compiler = new TypstCompiler(new TypstCompilerOptions
{
    PackagesSource = TypstPackagesSource.ProvidedOnly,
    FontsSource    = TypstFontsSource.DefaultOnly,
    CachePath      = "/var/cache/typst",
    DataPath       = "/var/lib/typst"
});
```

## TypstCompilerOptions

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `PackagesSource` | `TypstPackagesSource` | `All` | Where packages may be resolved from. |
| `FontsSource` | `TypstFontsSource` | `All` | Where fonts may be resolved from. |
| `CachePath` | `string?` | `null` | Package cache directory. `null` uses the default system cache path. |
| `DataPath` | `string?` | `null` | Package data directory. `null` uses the default system data path. |
| `RootPath` | `string?` | `null` | Real on-disk project root. Files not provided in-memory are read from here. `null` serves only in-memory files. See [Project Root & Fonts](project-root-and-fonts.md). |

## Package source modes

`TypstPackagesSource` controls how `@preview/...`-style package imports are resolved.

| Value | Behavior |
| --- | --- |
| `All` | Packages from the official repository **and** those provided manually via [`WithPackage`](examples.md#mocking-a-package). |
| `ProvidedOnly` | Only packages supplied manually via `WithPackage`. No network access. |
| `InternetOnly` | Only packages from the official online repository. |
| `None` | No packages allowed. |

## Font source modes

`TypstFontsSource` controls which fonts are available to the compiler.

| Value | Behavior |
| --- | --- |
| `All` | System fonts **and** the embedded default fonts. |
| `DefaultOnly` | Only the embedded default fonts. |
| `SystemOnly` | Only fonts installed on the host system. |
| `ProvidedOnly` | Only fonts supplied via [`WithFont`](examples.md#custom-fonts). |
| `None` | No fonts allowed. |

Fonts added per-job with `WithFont` are always available regardless of the mode; the mode governs which *ambient* font sources are also consulted.

## Cache and data paths

`CachePath` and `DataPath` redirect where Typst stores downloaded packages and related data. Pointing these at a known, writable directory is useful for:

- Containerized or sandboxed environments where the default system locations are read-only.
- Pre-warming a cache so repeated compilations do not re-download packages.

When left `null`, the platform default locations are used.

## On-disk project root

`RootPath` lets the compiler read project files (imports, images, data) from a real directory on disk instead of requiring everything in memory. It can also be set per compilation with `WithRoot`. See [Project Root & Fonts](project-root-and-fonts.md) for details and examples.

```csharp
using var compiler = new TypstCompiler(new TypstCompilerOptions
{
    RootPath = "/path/to/my-typst-project"
});
```

## Offline and hermetic setups

For reproducible builds with no network access, disable online package resolution and restrict fonts to what you ship:

```csharp
using var compiler = new TypstCompiler(new TypstCompilerOptions
{
    PackagesSource = TypstPackagesSource.None,        // no downloads at all
    FontsSource    = TypstFontsSource.DefaultOnly     // embedded fonts only
});
```

If you need packages but still want to stay offline, use `ProvidedOnly` and supply each package's files yourself with [`WithPackage`](examples.md#mocking-a-package):

```csharp
using var compiler = new TypstCompiler(new TypstCompilerOptions
{
    PackagesSource = TypstPackagesSource.ProvidedOnly
});

var result = compiler.Compile(c => c
    .WithSource("""#import "@preview/example:1.0.0": greet
                   #greet("World")""")
    .WithPackage("@preview/example:1.0.0", pkg => pkg
        .WithSource("lib.typ", "#let greet(name) = [Hello, #name!]")));
```

See [Examples](examples.md) for full, runnable snippets of each configurator method.
