# .NET Project Templates

## Creating projects and solutions

```bash
# New solution (produces .slnx in .NET 10)
dotnet new sln -n MySolution

# Force legacy .sln format (only if legacy tooling requires it)
dotnet new sln -n MySolution --format sln

# Common project templates
dotnet new console   -n MyApp
dotnet new classlib  -n MyCore
dotnet new webapi    -n MyApi
dotnet new blazor    -n MyWeb
dotnet new aspire-apphost  -n MyApp.AppHost
dotnet new nunit     -n MyApp.Tests

# Useful scaffold files
dotnet new gitignore
dotnet new editorconfig
dotnet new buildprops        # creates Directory.Build.props
dotnet new packagesprops     # creates Directory.Packages.props
dotnet new global.json       # pins SDK version

# Add a project to a solution
dotnet sln MySolution.slnx add src/MyApp.Api/MyApp.Api.csproj

# List all available templates
dotnet new list

# Search NuGet for template packs
dotnet new search <keyword>

# Install a template pack
dotnet new install <package-id>
```

---

## Built-in template reference

| Short name | Description |
|------------|-------------|
| `console` | Console application |
| `classlib` | Class library |
| `webapi` | ASP.NET Core Web API (Minimal API by default) |
| `web` | Empty ASP.NET Core project |
| `mvc` | ASP.NET Core MVC |
| `blazor` | Blazor Web App (full-stack server/WASM interactive) |
| `blazorwasm` | Blazor WebAssembly standalone |
| `blazorserver` | Blazor Server (legacy; prefer `blazor`) |
| `worker` | Background Worker Service |
| `grpc` | gRPC service |
| `aspire-apphost` | .NET Aspire App Host |
| `aspire-servicedefaults` | .NET Aspire Service Defaults library |
| `aspire-starter` | .NET Aspire starter solution |
| `nunit` | NUnit test project |
| `xunit` | xUnit test project |
| `mstest` | MSTest test project |
| `razorclasslib` | Razor Class Library |
| `globaljson` | `global.json` file |
| `gitignore` | `.gitignore` for .NET |
| `editorconfig` | `.editorconfig` for .NET |
| `buildprops` | `Directory.Build.props` |
| `packagesprops` | `Directory.Packages.props` |
| `sln` | Solution file (`.slnx` in .NET 10) |

---

## Key template options

### `webapi`
```bash
# Minimal API (default; preferred for new projects)
dotnet new webapi -n MyApi

# Controller-based (legacy style)
dotnet new webapi -n MyApi --use-controllers

# Skip HTTPS redirect (handy for container-first apps)
dotnet new webapi -n MyApi --no-https
```

### `blazor`
```bash
# Default: Interactive Auto render mode (server + WASM progressive)
dotnet new blazor -n MyWeb

# Server-only rendering
dotnet new blazor -n MyWeb --interactivity Server

# WASM-only rendering
dotnet new blazor -n MyWeb --interactivity WebAssembly

# Static SSR only (no interactivity)
dotnet new blazor -n MyWeb --interactivity None
```

### `global.json`
Pin the SDK so every developer and CI agent uses the same version:
```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestMinor"
  }
}
```
`rollForward: "latestMinor"` allows patch/minor updates automatically while
locking the major version — a good balance between stability and picking up
security fixes.

---

## Typical multi-project solution workflow

```bash
# 1. Create the solution
mkdir MyApp && cd MyApp
dotnet new sln -n MyApp

# 2. Create the scaffold files
dotnet new gitignore
dotnet new editorconfig
dotnet new buildprops
dotnet new packagesprops
dotnet new global.json

# 3. Create projects
dotnet new webapi  -n MyApp.Api          --output src/MyApp.Api
dotnet new classlib -n MyApp.Core        --output src/MyApp.Core
dotnet new nunit   -n MyApp.Tests        --output tests/MyApp.Tests

# 4. Add to solution with folder structure
dotnet sln MyApp.slnx add src/MyApp.Api/MyApp.Api.csproj       --solution-folder src
dotnet sln MyApp.slnx add src/MyApp.Core/MyApp.Core.csproj     --solution-folder src
dotnet sln MyApp.slnx add tests/MyApp.Tests/MyApp.Tests.csproj --solution-folder tests
```

---

## Custom templates

You can create reusable templates for your organisation and install them from a
local path or NuGet:
```bash
# Install from local directory
dotnet new install ./my-template-dir/

# Uninstall
dotnet new uninstall ./my-template-dir/
```

A template requires a `.template.config/template.json` descriptor at its root.
See https://learn.microsoft.com/dotnet/core/tools/custom-templates for the full
schema.
