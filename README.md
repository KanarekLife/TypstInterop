# TypstInterop

[![NuGet](https://img.shields.io/nuget/v/TypstInterop.svg)](https://www.nuget.org/packages/TypstInterop/)
[![Typst](https://img.shields.io/badge/Typst-0.15.0-239dad.svg)](https://github.com/typst/typst/releases/tag/v0.15.0)
[![Build and Pack](https://github.com/KanarekLife/TypstInterop/actions/workflows/build-and-pack.yml/badge.svg)](https://github.com/KanarekLife/TypstInterop/actions/workflows/build-and-pack.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

TypstInterop is a high-performance .NET bridge for [Typst](https://typst.app/), the modern document markup language. It links the Typst compilation engine directly into your process, so you can generate PDFs, images, and HTML from C# without shelling out to a CLI or depending on external services.

It currently embeds **Typst 0.15.0** and targets **.NET 8, 9, 10** and **.NET Framework 4.8**, on Windows, Linux (glibc and musl/Alpine), and macOS (x64/arm64; .NET Framework is Windows-only).

## Features

- In-memory compilation to **PDF, PNG, SVG, or HTML**.
- Fluent API for sources, assets, fonts, inputs, and mocked packages.
- PDF options (PDF/A, metadata, reproducible timestamps) and structured diagnostics.
- Deterministic and offline-capable — control or disable package/font resolution.

## Installation

```bash
dotnet add package TypstInterop
```

### Runtime identifier

The native Typst engine ships in per-platform `TypstInterop.runtime.<rid>` packages that the main package selects through a `runtime.json` RID graph. NuGet only restores those for a **specific runtime identifier**, so a consuming **application** must set one — otherwise the native is never restored and the app throws `DllNotFoundException: typst_interop` at startup.

```xml
<PropertyGroup>
  <!-- one RID, or use <RuntimeIdentifiers> for several -->
  <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
</PropertyGroup>
```

```bash
dotnet run     -r linux-x64
dotnet publish -r linux-x64
```

Supported RIDs: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, `osx-arm64`. Building an executable without a RID emits build warning `TYPST0001`. Class libraries don't need a RID — the final application picks it. See [Getting Started](wiki/getting-started.md#set-a-runtimeidentifier) for details.

## Quick Start

```csharp
using TypstInterop;

// The compiler is disposable and owns the native Typst engine life-cycle.
using var compiler = new TypstCompiler();

var result = compiler.Compile(c => c
    .WithSource("= Hello #sys.inputs.user!")
    .WithInput("user", "Developer"));

if (result.IsSuccess)
    File.WriteAllBytes("report.pdf", result.Output.ToArray());
else
    Console.WriteLine($"Compilation failed: {result.ErrorMessage}");
```

## Documentation

Full guides live in the [`wiki/`](wiki/home.md) folder:

- [Getting Started](wiki/getting-started.md) — install and compile your first document.
- [Output Formats](wiki/output-formats.md) — PDF/PNG/SVG/HTML and PDF options.
- [Configuration](wiki/configuration.md) — package/font sources, cache paths, offline builds.
- [Diagnostics & Error Handling](wiki/diagnostics.md) — errors and warnings.
- [Project Root & Fonts](wiki/project-root-and-fonts.md) — on-disk projects and font listing.
- [Examples](wiki/examples.md) — inputs, fonts, assets, and package mocking.
- [Building From Source](wiki/building-from-source.md) — toolchain and tests.
- [Benchmarks](wiki/benchmarks.md) — performance vs. other Typst .NET wrappers.

## License

This project is licensed under the [MIT License](LICENSE).
