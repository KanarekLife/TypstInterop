# CI / local-dev image for TypstInterop.
#
# This is a dual-toolchain image: it contains BOTH the .NET SDK and a stable
# Rust toolchain, because building the library invokes `cargo` from the MSBuild
# `BuildRust` target (so lint / test / native-build / pack all need both).
#
# Base: official Microsoft .NET SDK 10.0 image. The .NET 8.0 and 9.0 SDKs that
# some jobs also need are still installed via `actions/setup-dotnet` in the
# workflow (side-by-side), so the SDK version set stays in one place and
# matches the non-containerized configuration exactly.
#
# Rust is installed via rustup (the standard installer) pinned to the `stable`
# channel, plus the rustfmt/clippy components used by the lint job and the
# aarch64-unknown-linux-gnu target + cross linker used by the linux-arm64
# native build.
#
# ---------------------------------------------------------------------------
# Running locally (reproduce the lint/test CI environment):
#
#   docker build -f docker/ci.Dockerfile -t typstinterop-ci .
#   docker run --rm -it -v "$PWD":/work -w /work typstinterop-ci bash
#
# Then inside the container, e.g.:
#   dotnet format --verify-no-changes
#   cargo fmt --all -- --check          # (cd src/typst_interop)
#   cargo clippy --all-targets -- -D warnings
#   dotnet test src/TypstInterop.Tests/TypstInterop.Tests.csproj \
#       --filter "FullyQualifiedName!~Integration"
# ---------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:10.0

# rustup/cargo are installed system-wide. RUSTUP_HOME stays fixed so the
# toolchain is shared by all users, and the cargo bin dir is on PATH. We do NOT
# pin CARGO_HOME at runtime: leaving it unset lets cargo use the default
# ~/.cargo for its registry/git caches, which is exactly what the workflow's
# actions/cache step caches (path: ~/.cargo/registry, ~/.cargo/git).
ENV RUSTUP_HOME=/usr/local/rustup \
    PATH=/usr/local/cargo/bin:$PATH

# System packages: curl/ca-certs for rustup, build-essential for native linking,
# and the aarch64 cross toolchain for the linux-arm64 native build.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        ca-certificates \
        git \
        build-essential \
        pkg-config \
        gcc-aarch64-linux-gnu \
        libc6-dev-arm64-cross \
    && rm -rf /var/lib/apt/lists/*

# Install the stable Rust toolchain via rustup, with the components and target
# the CI jobs require. CARGO_HOME is set only for this layer so rustup/cargo and
# the installed binaries live under /usr/local/cargo (on PATH); at runtime
# CARGO_HOME is unset (see above) so cargo caches under the user's ~/.cargo.
RUN export CARGO_HOME=/usr/local/cargo \
    && curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs \
        | sh -s -- -y --default-toolchain stable --profile minimal \
    && rustup component add rustfmt clippy \
    && rustup target add aarch64-unknown-linux-gnu \
    && chmod -R a+w "$RUSTUP_HOME" "$CARGO_HOME"

# Cross linker for aarch64-unknown-linux-gnu (read by cargo via this env var).
ENV CARGO_TARGET_AARCH64_UNKNOWN_LINUX_GNU_LINKER=aarch64-linux-gnu-gcc

WORKDIR /work
