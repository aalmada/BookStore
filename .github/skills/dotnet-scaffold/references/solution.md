# Solution Files: .sln vs .slnx

## Why .slnx?

`.slnx` is the default solution format starting with .NET 10 (`dotnet new sln`
now produces `.slnx`). It replaces the legacy `.sln` format with clean, human-
readable XML.

| | `.sln` | `.slnx` |
|---|---|---|
| Format | Proprietary text | Standard XML |
| Human-readable | Barely | Completely |
| GUIDs | Required everywhere | None |
| Lines for 3 projects | ~35 | ~7 |
| Git merge conflicts | Frequent, painful | Rare, simple |
| Config platforms | Explicitly listed | Inferred from projects |
| .NET CLI default | .NET 9 and below | .NET 10+ |
| Tooling support | All | VS 2022 17.13+, Rider 2024.3+, VS Code, MSBuild 17.12+, CLI 9.0.200+ |

**Recommendation:** Use `.slnx` for all new solutions. Only fall back to `.sln`
if a specific unmigrated third-party tool parses `.sln` files directly.

---

## Format examples

**.sln** — 35 lines for 3 projects, full of GUIDs:
```
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "MyApp.Api", "src\MyApp.Api\MyApp.Api.csproj", "{A1B2C3D4-...}"
EndProject
...
GlobalSection(ProjectConfigurationPlatforms) = postSolution
  {A1B2C3D4-...}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
  ...
EndGlobalSection
```

**.slnx** — 7 lines, pure XML, no GUIDs:
```xml
<Solution>
  <Folder Name="src">
    <Project Path="src/MyApp.Api/MyApp.Api.csproj" />
    <Project Path="src/MyApp.Core/MyApp.Core.csproj" />
  </Folder>
  <Folder Name="tests">
    <Project Path="tests/MyApp.Tests/MyApp.Tests.csproj" />
  </Folder>
</Solution>
```

Nested solution folders use nested `<Folder>` elements — no GUIDs, no
`NestedProjects` section.

---

## Creating a solution

```bash
# .NET 10 — produces .slnx automatically
dotnet new sln -n MySolution

# Explicit .slnx
dotnet new sln -n MySolution --format slnx

# Force legacy .sln (avoid unless required)
dotnet new sln -n MySolution --format sln
```

---

## Managing projects

```bash
# Add with optional solution folder grouping
dotnet sln MySolution.slnx add src/MyApp.Api/MyApp.Api.csproj --solution-folder src
dotnet sln MySolution.slnx add tests/MyApp.Tests/MyApp.Tests.csproj --solution-folder tests

# Remove a project
dotnet sln MySolution.slnx remove src/MyApp.Api/MyApp.Api.csproj

# List projects in the solution
dotnet sln MySolution.slnx list
```

---

## Migrating from .sln to .slnx

```bash
# Requires .NET SDK 9.0.200+
dotnet --version   # verify first

# Migrate (original .sln is preserved; validate before deleting)
dotnet sln MyApp.sln migrate

# Verify the build still works
dotnet build MyApp.slnx
dotnet test  MyApp.slnx

# Remove the old file once validated
rm MyApp.sln
```

**Do not keep both `.sln` and `.slnx` in the same repository** — the CLI will
ask which one to use every time and CI will fail without an explicit path.

---

## CI/CD considerations

If your pipeline references the solution by name, update it:
```yaml
# Before
- run: dotnet build MyApp.sln

# After
- run: dotnet build MyApp.slnx
```

`dotnet build` / `dotnet test` without a solution name auto-discovers whichever
format is present, so pipelines using bare commands work without changes.

Docker — update the `COPY` instruction:
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["MyApp.slnx", "."]   # was MyApp.sln
```

---

## .gitignore recommendation

Once migrated, prevent the old format from accidentally being re-added:
```
# .gitignore
*.sln
```

Or name it explicitly if you still have other repositories using `.sln`:
```
MyApp.sln
```
