# Directory.Build.props and Directory.Build.targets

## What they are

`Directory.Build.props` and `Directory.Build.targets` are MSBuild files that
are automatically imported into every project in the directory tree beneath them.
The key difference is *when* they're imported:

| File | Import point | Use for |
|------|-------------|---------|
| `Directory.Build.props` | Early (before SDK defaults) | Properties that configure the SDK |
| `Directory.Build.targets` | Late (after NuGet targets) | Custom build targets, post-build actions |

Placing them at the repository root means all projects share the same settings
automatically — no property repetition in individual `.csproj` files.

Create them quickly via the CLI:
```bash
dotnet new buildprops   # creates Directory.Build.props
```

---

## Recommended Directory.Build.props for .NET 10

```xml
<Project>
  <PropertyGroup>
    <!-- Framework & Language -->
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- Code quality — all three enforce style/analysis at build time -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest</AnalysisLevel>

    <!-- Source generation — exposes generated files in obj/ for debugging -->
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>

    <!-- Package metadata (for libraries) -->
    <Authors>Your Name</Authors>
    <Copyright>Copyright (c) 2025 Your Name</Copyright>
  </PropertyGroup>

  <!-- Deterministic + SourceLink for reproducible builds -->
  <PropertyGroup>
    <Deterministic>true</Deterministic>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <!-- CI-only: reproducible builds on GitHub Actions -->
  <PropertyGroup Condition="'$(GITHUB_ACTIONS)' == 'true'">
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
  </PropertyGroup>

  <!-- Shared analyzer packages — applied to every project automatically -->
  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

---

## Property explanations

**`LangVersion=latest`** — automatically uses the latest C# version supported by
the installed SDK (C# 13 in .NET 9, C# 14 in .NET 10). Never pin to a specific
number; you'll miss new features unnecessarily.

**`Nullable=enable`** — enables nullable reference types. This is one of the
highest-value safety features in modern C#. With it enabled you'll get compile-
time warnings for potential null dereferences.

**`TreatWarningsAsErrors=true`** — turns all warnings into errors. Without this,
warnings accumulate silently and never get fixed. Pair it with `<NoWarn>` for
intentional suppression of specific warnings:
```xml
<NoWarn>$(NoWarn);CS1591</NoWarn>  <!-- suppress missing XML docs -->
```

**`EnforceCodeStyleInBuild=true`** — causes `.editorconfig` style rules to
produce build errors/warnings rather than just IDE squiggles. This makes code
style enforceable in CI.

**`AnalysisLevel=latest`** — enables the newest generation of Roslyn code
quality rules as soon as they're available in the SDK.

**`EmitCompilerGeneratedFiles=true`** — writes source-generated files (e.g.,
from `System.Text.Json`, `RegexGenerator`, `LoggerMessage`) into `obj/` so you
can inspect them. Harmless and very helpful for debugging generators.

---

## Directory.Build.targets

Use `.targets` for things that must run *after* the project and NuGet imports,
or that define build targets:

```xml
<Project>
  <!-- Enforce code analysis across all projects -->
  <PropertyGroup>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>

  <!-- Custom post-build target — e.g., log output path -->
  <Target Name="LogBuildOutput" AfterTargets="Build">
    <Message Importance="high" Text="Built $(MSBuildProjectName) → $(TargetPath)" />
  </Target>
</Project>
```

---

## Multi-level merging

By default MSBuild stops scanning upward after finding the first
`Directory.Build.props`. To support per-folder overrides that also inherit
from the root file, add this to the inner file:

```xml
<!-- tests/Directory.Build.props -->
<Project>
  <!-- Import the root settings first -->
  <Import Project="$([MSBuild]::GetPathOfFileAbove(
      'Directory.Build.props',
      '$(MSBuildThisFileDirectory)../'))"
    Condition="'' != $([MSBuild]::GetPathOfFileAbove(
      'Directory.Build.props',
      '$(MSBuildThisFileDirectory)../'))" />

  <!-- Test-only overrides -->
  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
</Project>
```

This is useful for applying different settings to `src/` vs `tests/`.

---

## Overriding in individual projects

Settings from `Directory.Build.props` are defaults. Any project can override
them by setting the property in its own `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- This project targets multiple frameworks -->
    <TargetFrameworks>net10.0;netstandard2.0</TargetFrameworks>

    <!-- Disable TreatWarningsAsErrors for generated code projects -->
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

---

## Troubleshooting

**Property not taking effect**: Run `dotnet msbuild /pp:preprocessed.xml MyProj.csproj`
to see the full merged file with import order. Look for where your property is set
vs where it's overridden.

**File silently ignored on Linux/macOS**: The filename must match exactly —
`Directory.Build.props` (capital B and capital P). Linux filesystems are
case-sensitive.

**Visual Studio not picking up changes**: Close and reopen the solution, or
right-click → Reload Project after editing `.props` or `.targets` files.
