# NuGet Central Package Management (Directory.Packages.props)

## Why CPM?

In a multi-project solution, each project independently specifying
`<PackageReference Include="Newtonsoft.Json" Version="13.0.1" />` leads to
version drift — different projects accidentally pick up different versions.
Central Package Management (CPM) fixes this by declaring every version in one
authoritative file: `Directory.Packages.props`.

---

## Getting started

```bash
# Create the file via CLI (SDK 7+ required)
dotnet new packagesprops
```

This produces a `Directory.Packages.props` at the current directory.

---

## Directory.Packages.props structure

```xml
<Project>
  <PropertyGroup>
    <!-- Enable CPM for all projects in this directory tree -->
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Declare versions here — no actual references, just versions -->
    <PackageVersion Include="Newtonsoft.Json"        Version="13.0.3" />
    <PackageVersion Include="Serilog.AspNetCore"     Version="8.0.3" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.4" />
  </ItemGroup>
</Project>
```

---

## In project files — omit the Version attribute

```xml
<!-- src/MyApp.Api/MyApp.Api.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <!-- No Version attribute — resolved from Directory.Packages.props -->
    <PackageReference Include="Newtonsoft.Json" />
    <PackageReference Include="Serilog.AspNetCore" />
  </ItemGroup>
</Project>
```

Having `Version` on a `<PackageReference>` when CPM is enabled causes **error
NU1008**. Remove the attribute.

---

## Version overrides (use sparingly)

If one project genuinely needs a different version of a package:

```xml
<!-- Only in project file, not in Directory.Packages.props -->
<PackageReference Include="Newtonsoft.Json" VersionOverride="12.0.1" />
```

To prevent this escape hatch from being misused across the team, disable it:
```xml
<!-- Directory.Packages.props -->
<PropertyGroup>
  <CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled>
</PropertyGroup>
```

---

## Global package references

For packages that every project should receive (e.g., analyzer-only packages,
versioning tools), use `GlobalPackageReference` instead of repeating it in
every project file:

```xml
<!-- Directory.Packages.props -->
<ItemGroup>
  <!-- Applied to ALL projects automatically -->
  <GlobalPackageReference Include="Roslynator.Analyzers" Version="4.12.9"
    IncludeAssets="runtime; build; native; contentfiles; analyzers"
    PrivateAssets="all" />
</ItemGroup>
```

This replaces placing `<PackageReference>` inside `Directory.Build.props` (which
also works but `GlobalPackageReference` makes the intent clearer).

---

## Transitive pinning

Prevent "diamond dependency" surprises by explicitly pinning a transitive
dependency to a known-safe version:

```xml
<!-- Directory.Packages.props -->
<PropertyGroup>
  <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
</PropertyGroup>
<ItemGroup>
  <!-- Even though nothing in the solution directly references this,
       pin it to override whatever version a transitive dep would pull in -->
  <PackageVersion Include="System.Text.Json" Version="9.0.4" />
</ItemGroup>
```

---

## Opting a single project out of CPM

```xml
<!-- SomeSpecialProject.csproj -->
<PropertyGroup>
  <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
</PropertyGroup>
```

---

## Multi-repo / subdirectory overrides

`Directory.Packages.props` follows the same "walk upward, stop at first match"
rule as `Directory.Build.props`. If a project is in a subdirectory with its own
`Directory.Packages.props`, that file is used instead of the root one.

To inherit from the root and add overrides:
```xml
<!-- sub-solution/Directory.Packages.props -->
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove(
      'Directory.Packages.props',
      '$(MSBuildThisFileDirectory)../'))" />
  <ItemGroup>
    <!-- Override one version for this sub-solution -->
    <PackageVersion Update="Newtonsoft.Json" Version="12.0.1" />
  </ItemGroup>
</Project>
```

Note: use `Update` (not `Include`) when overriding an entry that was already
declared by an imported file.

---

## Keeping versions up to date

```bash
# List outdated packages (requires dotnet-outdated-tool)
dotnet outdated

# Or use the NuGet Package Manager in Visual Studio / Rider to see upgrade
# suggestions — they respect the centralised versions file.
```

---

## Common errors

| Error | Cause | Fix |
|-------|-------|-----|
| **NU1008** | `<PackageReference>` has a `Version` attribute while CPM is on | Remove `Version` from `<PackageReference>` |
| **NU1604** | `<PackageVersion>` entry exists but has no `Version` | Add `Version="..."` to the `<PackageVersion>` |
| **NU1507** | Multiple package sources defined with CPM | Use package source mapping or single source |
