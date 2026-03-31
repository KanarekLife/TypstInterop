# TypstInterop

A high-performance .NET interop library for [Typst](https://typst.app/), providing a native bridge to the Typst compilation engine.

## Features

- **Native Performance**: Direct FFI calls to a Rust-based Typst wrapper.
- **Cross-Platform**: Supports Windows, Linux, and macOS (x64 and ARM64).
- **Fluent API**: Easy-to-use builder pattern for compilation jobs.
- **VFS Support**: Compile from memory, streams, or disk.
- **Package Management**: Integrated support for Typst packages.
- **Multi-Target**: Supports .NET Framework 4.8, .NET 8.0, 9.0, and 10.0.

## Installation

```bash
dotnet add package TypstInterop
```

## Quick Start

```csharp
using TypstInterop;

// Simple compilation
using var compiler = new TypstCompiler();
var result = compiler.Compile(c => c.WithSource("= Hello from Typst!"));

if (result.IsSuccess)
{
    File.WriteAllBytes("output.pdf", result.GetBytes());
}
else
{
    Console.WriteLine($"Compilation failed: {result.ErrorMessage}");
}
```

### Advanced Usage (with assets)

```csharp
using var compiler = new TypstCompiler();
var result = compiler.Compile(c => c
    .WithSource(@"
        #import ""logo.png""
        = Project Report
        Hello #sys.inputs.name!
    ")
    .WithFile("logo.png", File.ReadAllBytes("logo.png"))
    .WithInput("name", "John Doe"));

if (result.IsSuccess)
{
    File.WriteAllBytes("output.pdf", result.GetBytes());
}
```

## Building

The project uses a hybrid build system (Rust + .NET).

### Prerequisites
- [Rust toolchain](https://rustup.rs/)
- [.NET SDK (8.0+)](https://dotnet.microsoft.com/download)

### Local Development
To build the project for your current platform:
```bash
dotnet build
```

### Multi-Platform Packaging
The project includes a GitHub Actions workflow that automatically builds native binaries for all supported platforms (Windows, Linux, macOS) and bundles them into a single NuGet package.

To manually build for a specific platform:
```bash
# Example for Linux ARM64
dotnet build TypstInterop/TypstInterop.csproj -c Release -r linux-arm64 -p:RustTarget=aarch64-unknown-linux-gnu
```

## License

MIT
