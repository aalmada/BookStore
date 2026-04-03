---
name: dotnet-scaffold
description: Use this skill when scaffolding new .NET solutions or projects, configuring shared build properties, setting up NuGet Central Package Management, enforcing code style with .editorconfig, or choosing between .sln and .slnx solution formats. Always trigger when users ask about dotnet new templates, multi-project solution layout, Directory.Build.props, Directory.Packages.props, .editorconfig, global.json, TreatWarningsAsErrors, LangVersion, or anything about organizing a .NET repository from scratch—even if they don't mention any of those file names explicitly.
---

# .NET Scaffolding Skill

Use this skill to scaffold modern .NET solutions and projects correctly. The
non-obvious parts are covered in the reference files below — read the relevant
ones before generating or modifying any scaffolding files.

---

## Quick decision tree

| Question | Go to |
|----------|-------|
| Which `dotnet new` template to use? What options exist? | [references/templates.md](references/templates.md) |
| Create or manage a solution file? sln vs slnx? | [references/solution.md](references/solution.md) |
| Set properties that apply to all projects in the repo? | [references/build-props.md](references/build-props.md) |
| Centralize NuGet versions in one place? | [references/packages-props.md](references/packages-props.md) |
| Enforce coding style and formatting rules? | [references/editorconfig.md](references/editorconfig.md) |

---

## Modern defaults to always apply

These are the baseline quality settings for every new .NET 10 solution. Do not
omit them — they represent the minimum for professional .NET development today.

**In `Directory.Build.props`:**
```xml
<TargetFramework>net10.0</TargetFramework>
<LangVersion>latest</LangVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<AnalysisLevel>latest</AnalysisLevel>
```

Every setting has a reason:
- `LangVersion=latest` — gets C# 13/14 features without waiting for SDK bumps
- `Nullable=enable` — null safety enforced at compile time
- `TreatWarningsAsErrors=true` — stops warnings being silently ignored
- `EnforceCodeStyleInBuild=true` — `.editorconfig` style rules fail the build
- `AnalysisLevel=latest` — enables the newest Roslyn analyzer rules

**Use `.slnx`** for any new solution (it's the .NET 10 default). See
[references/solution.md](references/solution.md) for details and migration.

**Enable Central Package Management** for any multi-project solution. See
[references/packages-props.md](references/packages-props.md).

---

## Common project skeleton

```
repo/
├── .editorconfig               ← code style rules (root = true)
├── .gitignore                  ← standard .NET gitignore
├── global.json                 ← pin the SDK version
├── Directory.Build.props       ← shared MSBuild properties (early)
├── Directory.Build.targets     ← shared MSBuild targets (late)
├── Directory.Packages.props    ← central NuGet versions
├── MySolution.slnx             ← solution file (.NET 10 default format)
├── src/
│   ├── MyApp.Api/
│   │   └── MyApp.Api.csproj
│   └── MyApp.Core/
│       └── MyApp.Core.csproj
└── tests/
    └── MyApp.Tests/
        └── MyApp.Tests.csproj
```

---

## Rules

```
✅ .slnx                             ❌ .sln (for new solutions)
✅ Directory.Build.props             ❌ Repeating <TargetFramework> in every .csproj
✅ Directory.Packages.props          ❌ Version attributes on <PackageReference>
✅ <LangVersion>latest</LangVersion> ❌ Pinning to a specific version number
✅ <Nullable>enable</Nullable>       ❌ Missing or disabled nullable
✅ file-scoped namespaces            ❌ braced namespace blocks
```
