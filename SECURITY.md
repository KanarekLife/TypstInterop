# Security Policy

## Supported versions

TypstInterop follows a roll-forward support model: security fixes are made
against the latest released version on the `main` branch and shipped in a new
release. Older releases are not patched in place.

| Version            | Supported          |
| ------------------ | ------------------ |
| Latest release     | :white_check_mark: |
| Previous releases  | :x:                |

If you depend on an older release, upgrade to the latest version to receive
security fixes.

## Reporting a vulnerability

**Please do not open public issues for security vulnerabilities.**

Report vulnerabilities privately through GitHub Security Advisories:

1. Go to the repository's **Security** tab:
   <https://github.com/KanarekLife/TypstInterop/security/advisories>
2. Click **Report a vulnerability** to open a private advisory.

The maintainer is **Stanisław Nieradko** (<https://github.com/KanarekLife>).

When reporting, please include:

- A description of the issue and its impact.
- Steps to reproduce (a minimal Typst document / package and the
  `TypstInterop` API calls involved are ideal).
- The affected version(s) and platform/runtime identifier (RID).

### Expected response

- Acknowledgement of your report within **7 days**.
- An initial assessment (severity, affected versions) within **14 days**.
- Coordinated disclosure: a fix and public advisory are published together,
  crediting the reporter unless anonymity is requested.

## Threat model and untrusted input

> **TL;DR — Typst input is untrusted code.** If you compile Typst markup or
> Typst Universe packages that you did not author, run those compilations in an
> OS-level sandbox or a separate process with enforced resource limits.

TypstInterop embeds the Typst compiler and runs it **in-process** inside the
host application (a native library is loaded via P/Invoke into your .NET
process). This has important security implications:

- **In-process compilation, no built-in limits.** Typst markup is a full
  programming language (scripting, loops, recursion). The compiler runs inside
  your process with **no built-in timeout and no built-in memory cap**. A
  malicious or pathological document can consume unbounded CPU and memory and
  affect the stability of the entire host process. TypstInterop does not
  sandbox compilation for you.

- **Treat all Typst input as untrusted unless you authored it.** Both the
  source markup *and* any imported Typst Universe packages execute as part of
  compilation. If your application compiles documents or templates supplied by
  end users (or pulls packages chosen at runtime), you **must** isolate the
  work:
  - Run untrusted compilations in a **separate process** that you can kill on a
    timeout, and/or inside an **OS-level sandbox** (container, seccomp,
    cgroups/job objects, ulimits) with explicit **CPU, memory, wall-clock, and
    file-descriptor limits**.
  - Never compile untrusted input directly on a thread that shares fate with
    the rest of your service.

- **Hardening switches: `PackagesSource` and `FontsSource`.** These let you
  control what the compiler can reach:
  - Use the **`None`** mode to disable package downloading / font discovery
    entirely, producing an offline, hermetic compilation that cannot fetch code
    or fonts from the network or arbitrary disk locations.
  - Restrict these sources to vetted, local-only locations when you need
    packages or fonts but want to avoid runtime network access to Typst
    Universe.
  Prefer the most restrictive (`None`) mode that still meets your needs when
  handling untrusted input.

- **Network access.** With the default package/font sources, compilation may
  reach out to Typst Universe to download packages. Downloaded package code is
  untrusted and executes during compilation. Disable this with the `None` mode
  for hermetic builds.

## Bundled TLS (vendored OpenSSL)

The shipped native binary statically bundles **OpenSSL** (built with the
`vendor-openssl` feature) rather than linking the host system's OpenSSL. This
means **TypstInterop is the patch vendor for that TLS stack**: OpenSSL security
fixes reach you only when a new TypstInterop release rebuilds against an updated
OpenSSL. We monitor OpenSSL advisories and ship updated binaries; if you become
aware of a relevant OpenSSL CVE, please report it through the channel above so a
refreshed release can be cut.
