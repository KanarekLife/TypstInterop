# TypstInterop

TypstInterop is a high-performance .NET bridge for [Typst](https://typst.app/), the modern document markup language. It provides a native, low-latency interface to the Typst compilation engine, allowing you to generate professional PDFs directly from your C# applications without external dependencies or CLI wrappers.

While many libraries struggle with legacy support, TypstInterop is built for the entire .NET ecosystem. It offers first-class support for **.NET Framework 4.8** alongside modern **.NET 8, 9, and 10**. Whether you are maintaining a reliable enterprise system or building a cutting-edge cloud service, TypstInterop provides a consistent and powerful experience.

We are committed to true cross-platform development. TypstInterop fully supports **Windows, Linux, and macOS** on both **x64 and ARM64** architectures. From Windows Server to Apple Silicon M-series chips and Linux-based Docker containers, your document generation will work seamlessly everywhere.

## Quick Start

You can compile documents entirely in memory, provide custom assets (like images or data files), and pass dynamic inputs into your Typst templates with a simple, fluent API.

```csharp
using TypstInterop;

// The compiler is disposable and manages the native Typst engine life-cycle
using var compiler = new TypstCompiler();

var result = compiler.Compile(c => c
    .WithSource(@"
        #import ""header.typ"": project_title
        = #project_title
        Hello #sys.inputs.user!
        #image(""logo.png"", width: 20%)
    ")
    .WithSource("header.typ", " #let project_title = [Automated Report] ")
    .WithFile("logo.png", File.ReadAllBytes("assets/logo.png"))
    .WithInput("user", "Developer"));

if (result.IsSuccess)
{
    // Access the raw PDF bytes
    byte[] pdf = result.GetBytes();
    File.WriteAllBytes("report.pdf", pdf);
}
else
{
    Console.WriteLine($"Compilation failed: {result.ErrorMessage}");
}
```

## Installation

Add the library to your project via NuGet:

```bash
dotnet add package TypstInterop
```

## License

This project is licensed under the MIT License.
