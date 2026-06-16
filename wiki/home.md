# TypstInterop

TypstInterop is a high-performance .NET bridge for [Typst](https://typst.app/), the modern document markup language. It links the Typst compilation engine directly into your process, so you can generate PDFs (and PNG, SVG, or HTML) from C# without shelling out to a CLI or depending on external services.

It currently embeds **Typst 0.15.0**.

## Features

- **In-memory compilation** — provide sources, assets, fonts, and inputs from memory; get back the output bytes.
- **Multiple output formats** — export to PDF, PNG, or SVG (one image per page), or experimental HTML.
- **PDF options** — PDF/A and PDF/UA standards, document title/author metadata, and a fixed creation timestamp for reproducible builds.
- **Structured diagnostics** — errors and warnings with severity, file, line/column, and hints.
- **On-disk project root** — compile a real Typst project directory via `WithRoot` / `RootPath`.
- **Font listing** — enumerate available font families with `ListFonts`.
- **Fluent API** — configure each compilation job with a small, chainable builder.
- **Broad target support** — .NET 8, 9, 10, and .NET Framework 4.8.
- **Cross-platform** — Windows, Linux (glibc and musl/Alpine), and macOS on x64 and arm64 (musl is x64-only; .NET Framework is Windows-only).
- **Deterministic & offline-capable** — control package and font resolution, or turn the network off entirely.

## Supported platforms

| OS | x64 | arm64 |
| --- | :---: | :---: |
| Windows | Yes | Yes |
| Linux (glibc) | Yes | Yes |
| Linux (musl/Alpine) | Yes | No |
| macOS | Yes | Yes |

The matching native binary is delivered automatically through per-RID `TypstInterop.runtime.*` packages.

| Target framework | Notes |
| --- | --- |
| `net8.0` | Cross-platform (Windows / Linux / macOS, x64 + arm64) |
| `net9.0` | Cross-platform |
| `net10.0` | Cross-platform |
| `net48` (.NET Framework 4.8) | Windows only |

## Documentation

- [Getting Started](getting-started.md) — install and compile your first PDF.
- [Configuration](configuration.md) — `TypstCompilerOptions`, package/font sources, project root, offline builds.
- [Output Formats](output-formats.md) — PDF, PNG, SVG, and HTML export plus PDF/A and metadata options.
- [Diagnostics & Error Handling](diagnostics.md) — `IsSuccess`, `Errors`, `Warnings`, and structured diagnostics.
- [Project Root & Fonts](project-root-and-fonts.md) — compile an on-disk project and list available fonts.
- [Examples](examples.md) — inputs, custom fonts, multiple sources, assets, and package mocking.
- [Building From Source](building-from-source.md) — build the native library and run tests.
- [Benchmarks](benchmarks.md) — performance vs. competing Typst .NET wrappers.

## How it works

The Typst compiler is compiled to a small native library (`libtypst_interop`) via a Rust FFI layer. The managed package resolves and loads the correct binary for the current OS/architecture at runtime from its companion `TypstInterop.runtime.<rid>` package.

## License

This project is licensed under the MIT License.
