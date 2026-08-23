# Phase 0 — Toolchain setup

## What got installed

- **.NET 10 SDK** (10.0.400) via `winget install --id Microsoft.DotNet.SDK.10`. .NET 10 is the current LTS release (supported until ~Nov 2028), which is why it was picked over .NET 9 (an STS release with a much shorter support window) even though the project started on 9 before switching.
- **VS Code C# Dev Kit** (`ms-dotnettools.csdevkit`) via `code --install-extension`. Pulls in the base C# extension and the .NET runtime install extension alongside it.
- Docker was already installed on the machine — not used yet, comes into play in Phase 2.

## Concepts

- **SDK vs. runtime**: the SDK includes the runtime plus the compiler, CLI (`dotnet`), and project templates. You only need the SDK for development; deployed apps can ship with just the runtime (or self-contained, with none at all).
- **`dotnet --list-sdks`**: shows every SDK version installed side-by-side — .NET lets multiple major versions coexist on one machine without conflict, each project picks its version via `<TargetFramework>` in its `.csproj`.
- **winget**: Windows' built-in package manager (`winget install <id>`), the Windows equivalent of `apt`/`brew`. No need to manually download installers from the .NET site.

## Why this order mattered

Started with .NET 9 SDK, then hit a wall immediately in Phase 1: the latest `Microsoft.EntityFrameworkCore.Sqlite` NuGet package (10.0.11) only supports `net10.0`, not `net9.0`. Rather than pin an older EF Core version to match .NET 9, switched to the .NET 10 SDK and retargeted the project — better to be on the current LTS from day one of a resume project anyway.
