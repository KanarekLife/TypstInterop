# Building From Source

TypstInterop is a managed .NET library wrapping a native Rust FFI layer. Building from source therefore requires both toolchains.

## Prerequisites

- **.NET SDK** — capable of building the targeted frameworks (.NET 8/9/10 and, on Windows, .NET Framework 4.8).
- **Rust toolchain** — install via [rustup](https://rustup.rs/). The managed build invokes `cargo` automatically.

## Building

From the repository root:

```bash
dotnet build
```

This first compiles the native library (`libtypst_interop`) for the host platform via `cargo`, then builds the managed assemblies. The first build is slower because Rust compiles the Typst engine from scratch; subsequent builds reuse the cargo cache.

To restore the solution without building (fast validation that the projects are coherent):

```bash
dotnet restore TypstInterop.slnx
```

## Running tests

Run the test suite, skipping the slower integration tests:

```bash
dotnet test --filter "FullyQualifiedName!~Integration"
```

Run everything, including integration tests:

```bash
dotnet test
```

## Solution layout

| Project | Purpose |
| --- | --- |
| `src/TypstInterop` | The managed library. |
| `src/TypstInterop.Runtime` | Native runtime packaging. |
| `src/TypstInterop.Tests` | Unit and integration tests. |
| `src/TypstInterop.Benchmarks` | Performance benchmarks. |

## Cross-platform notes

The native binary is platform-specific. A local `dotnet build` produces the binary for your current OS and architecture only. The published NuGet packages ship per-RID `TypstInterop.runtime.<rid>` packages covering Windows, Linux, and macOS on x64 and arm64; .NET Framework 4.8 is Windows-only.
