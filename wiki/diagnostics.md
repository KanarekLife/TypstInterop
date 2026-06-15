# Diagnostics & Error Handling

Every `Compile` call returns a `TypstCompilationResult` (namespace `TypstInterop.Models`) that carries both the produced output and any diagnostics the compiler emitted. Diagnostics are structured: each one has a severity, message, source position, and optional hints, so you can surface precise feedback rather than a single blob of text.

## TypstCompilationResult

| Member | Type | Description |
| --- | --- | --- |
| `IsSuccess` | `bool` | `true` when compilation produced output (no fatal errors). |
| `Diagnostics` | `IReadOnlyList<TypstDiagnostic>` | All diagnostics: errors **and** warnings. |
| `Errors` | `IEnumerable<TypstDiagnostic>` | Only the diagnostics with `Error` severity. |
| `Warnings` | `IEnumerable<TypstDiagnostic>` | Only the diagnostics with `Warning` severity. |
| `ErrorMessage` | `string?` | The concatenated error text when `IsSuccess` is `false`; otherwise `null`. |
| `Outputs` | `IReadOnlyList<byte[]>` | The produced outputs (see [Output Formats](output-formats.md)). |
| `GetBytes()` | `byte[]` | The first output (empty array on failure). |
| `GetStream()` | `Stream` | The first output (`Stream.Null` on failure). |

A successful compilation can still carry warnings, so `Warnings` is worth inspecting even when `IsSuccess` is `true`.

## TypstDiagnostic

| Member | Type | Description |
| --- | --- | --- |
| `Severity` | `TypstDiagnosticSeverity` | `Error` or `Warning`. |
| `Message` | `string` | The diagnostic message. |
| `FilePath` | `string?` | The file the diagnostic points into, or `null` when not tied to a file. |
| `Line` | `int` | 1-based line number, or `0` when unavailable. |
| `Column` | `int` | 1-based column number, or `0` when unavailable. |
| `Hints` | `string?` | Additional newline-separated hints, or `null`. |

`TypstDiagnostic.ToString()` formats the diagnostic as `file:line:column: severity: message` (the location prefix is omitted when no file/position is available).

`TypstDiagnosticSeverity`:

| Value | Meaning |
| --- | --- |
| `Error` | A fatal error that prevented output from being produced. |
| `Warning` | A non-fatal warning. Output is still produced. |

## Handling failures

```csharp
var result = compiler.Compile(c => c.WithSource(source));

if (!result.IsSuccess)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine(
            $"{error.FilePath}:{error.Line}:{error.Column} {error.Message}");
        if (error.Hints is not null)
            Console.WriteLine($"  hint: {error.Hints}");
    }
    throw new InvalidOperationException(result.ErrorMessage);
}
```

`ErrorMessage` is a convenience that joins the `Errors` together with newlines, which is handy for logging or rethrowing.

## Inspecting warnings on success

```csharp
var result = compiler.Compile(c => c.WithSource(source));

foreach (var warning in result.Warnings)
    Console.WriteLine($"warning: {warning}"); // uses ToString()

if (result.IsSuccess)
    File.WriteAllBytes("out.pdf", result.GetBytes());
```

## Surfacing all diagnostics

To present everything the compiler reported regardless of outcome, iterate `Diagnostics` and branch on `Severity`:

```csharp
foreach (var diagnostic in result.Diagnostics)
{
    var level = diagnostic.Severity == TypstDiagnosticSeverity.Error ? "ERROR" : "WARN";
    Console.WriteLine($"[{level}] {diagnostic}");
}
```

## See also

- [Output Formats](output-formats.md) — `Outputs`, formats, and PDF options (PDF/A errors surface here).
- [Getting Started](getting-started.md) — the basic success/failure flow.
