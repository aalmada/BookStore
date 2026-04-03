# .editorconfig for .NET Projects

## What it does

`.editorconfig` configures two distinct categories of rules:

1. **Editor formatting** — indentation, line endings, charset, trailing
   whitespace. These apply in any editor that supports EditorConfig.
2. **.NET / C# code style** — language rules (var vs explicit types, expression
   bodies, pattern matching, etc.) and Roslyn naming rules. These are enforced
   at build time when `EnforceCodeStyleInBuild=true` is set in
   `Directory.Build.props`.

Create it quickly:
```bash
dotnet new editorconfig
```

---

## Key settings

### `root = true`

Put `root = true` at the top of the file in the repository root. This tells
editors to stop searching parent directories for additional `.editorconfig`
files. Sub-directories can have their own files to override specific rules.

---

## Recommended .editorconfig for modern .NET / C# 14

```ini
root = true

# All files
[*]
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true
indent_style = space
indent_size = 4

# C# and VB
[*.{cs,vb}]

#### Naming rules ####

# Interfaces: IPascalCase
dotnet_naming_rule.interface_should_begin_with_i.symbols  = interface
dotnet_naming_rule.interface_should_begin_with_i.style    = begins_with_i
dotnet_naming_rule.interface_should_begin_with_i.severity = warning

dotnet_naming_symbols.interface.applicable_kinds          = interface
dotnet_naming_symbols.interface.applicable_accessibilities = public, internal, private, protected

dotnet_naming_style.begins_with_i.required_prefix        = I
dotnet_naming_style.begins_with_i.capitalization         = pascal_case

# Types: PascalCase
dotnet_naming_rule.types_should_be_pascal_case.symbols  = types
dotnet_naming_rule.types_should_be_pascal_case.style    = pascal_case
dotnet_naming_rule.types_should_be_pascal_case.severity = warning

dotnet_naming_symbols.types.applicable_kinds            = class, struct, interface, enum
dotnet_naming_style.pascal_case.capitalization          = pascal_case

#### Organize usings ####
dotnet_sort_system_directives_first                     = true:warning
dotnet_separate_import_directive_groups                 = false:warning

#### Language rules ####

# File-scoped namespaces (C# 10+)
csharp_style_namespace_declarations                     = file_scoped:warning

# var preferences
csharp_style_var_for_built_in_types                     = true:warning
csharp_style_var_when_type_is_apparent                  = true:warning
csharp_style_var_elsewhere                              = true:warning

# Modern C# features
csharp_style_prefer_primary_constructors                = true:suggestion
csharp_style_prefer_collection_expression               = when_types_exactly_match:warning
dotnet_style_prefer_collection_expression               = when_types_exactly_match:warning
csharp_style_implicit_object_creation_when_type_is_apparent = true:warning

# Expression-bodied members
csharp_style_expression_bodied_methods                  = true:warning
csharp_style_expression_bodied_properties               = true:warning
csharp_style_expression_bodied_constructors             = true:warning
csharp_style_expression_bodied_accessors                = true:warning
csharp_style_expression_bodied_lambdas                  = true:warning
csharp_style_expression_bodied_local_functions          = true:warning
csharp_style_expression_bodied_operators                = true:warning
csharp_style_expression_bodied_indexers                 = true:warning

# Pattern matching
csharp_style_pattern_matching_over_is_with_cast_check  = true:warning
csharp_style_pattern_matching_over_as_with_null_check  = true:warning
csharp_style_prefer_switch_expression                   = true:warning
csharp_style_prefer_not_pattern                         = true:warning
csharp_style_prefer_pattern_matching                    = true:warning
csharp_style_prefer_extended_property_pattern           = true:warning

# Null checking
csharp_style_throw_expression                           = true:warning
csharp_style_prefer_null_check_over_type_check          = true:warning
csharp_style_conditional_delegate_call                  = true:warning

# Ranges and indices (C# 8+)
csharp_style_prefer_index_operator                      = true:warning
csharp_style_prefer_range_operator                      = true:warning

# Readonly structs (C# 8+)
csharp_style_prefer_readonly_struct                     = true:warning
csharp_style_prefer_readonly_struct_member              = true:warning

# Misc modern preferences
csharp_prefer_braces                                    = true:warning
csharp_prefer_simple_using_statement                    = true:warning
csharp_style_unused_value_expression_statement_preference = discard_variable:error

# Accessibility modifiers
dotnet_style_require_accessibility_modifiers            = omit_if_default:warning

# Auto-properties
dotnet_style_prefer_auto_properties                     = true:warning
dotnet_style_prefer_simplified_boolean_expressions      = true:warning
dotnet_style_prefer_compound_assignment                 = true:warning
dotnet_style_prefer_simplified_interpolation            = true:warning

# Tuple names
dotnet_style_prefer_inferred_tuple_names                = true:warning
dotnet_style_prefer_inferred_anonymous_type_member_names = true:warning

# Parameter hygiene
dotnet_code_quality_unused_parameters                   = all:warning

# Namespace matches folder (suggestion to not block builds on new files)
dotnet_style_namespace_match_folder                     = true:suggestion

# Blank line hygiene
dotnet_style_allow_multiple_blank_lines_experimental    = false:warning
dotnet_style_allow_statement_immediately_after_block_experimental = false:warning

# Logging — prefer [LoggerMessage] source generator
dotnet_diagnostic.CA1848.severity                       = warning
```

---

## Why `csharp_style_unused_value_expression_statement_preference = discard_variable:error`

This rule (diagnostic [IDE0058](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0058)) is set to **`:error`** — not `:warning` — because silently discarding a return value can cause a runtime bug that compiles cleanly.

Classic example: changing from `List<T>` to `ImmutableList<T>`:

```csharp
// List<T>.Add returns void — discarding the call is correct
var list = new List<int>();
list.Add(42); // fine

// ImmutableList<T>.Add returns a NEW instance — the original is unchanged!
var immutable = ImmutableList.Create<int>();
immutable.Add(42); // silent bug: result is thrown away, immutable is still empty
```

The refactoring compiles without warning. At runtime, nothing is added. With `IDE0058 = error`, the compiler flags the call immediately.

When you genuinely intend to ignore a return value (e.g., fluent APIs used for assertions), use an explicit discard to make the intent clear:

```csharp
// Explicit discard — communicates "yes, I know there's a return value, I don't need it"
_ = result.Must()
    .BeEnumerableOf<string>()
    .BeEqualTo(expected);
```

> Reference: [Defensive Coding in C#: A Closer Look at Unchecked Return Value Discards](https://aalmada.github.io/posts/A-closer-look-at-unchecked-return-value-discards/)

---

## Severity levels

| Severity | Meaning |
|----------|---------|
| `none` | Rule disabled |
| `silent` / `refactoring` | Available as code fix, no squiggle |
| `suggestion` | Blue squiggle, Quick Fix available |
| `warning` | Yellow squiggle; build warning when `EnforceCodeStyleInBuild=true` |
| `error` | Red squiggle; build error (blocks CI) |

Use `warning` for style preferences that matter but aren't blocking. Use `error`
only for rules you're absolutely certain you never want to break (e.g., unused
discard variables).

---

## Build-time enforcement

For `.editorconfig` styles to block a build, two things must both be true:

1. `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` in
   `Directory.Build.props`
2. The rule severity is `warning` or `error` in `.editorconfig`

Check compliance without building:
```bash
dotnet format --verify-no-changes
```

Fix all auto-fixable violations:
```bash
dotnet format
```

Fix only whitespace:
```bash
dotnet format whitespace
```

Fix only style rules:
```bash
dotnet format style
```

---

## Sub-directory overrides

If a sub-project needs a different rule (e.g., a generated-code project that
cannot satisfy certain naming rules), add a nested `.editorconfig` without
`root = true`:

```ini
# src/Generated/.editorconfig  — no "root = true" so it inherits from root
[*.cs]
# Turn off naming rules for generated code
dotnet_naming_rule.interface_should_begin_with_i.severity = none
dotnet_naming_rule.types_should_be_pascal_case.severity   = none
```

MSBuild merges from root down, with the most specific file winning.

---

## Suppressing specific diagnostic IDs

When a rule needs to be suppressed project-wide with a rationale comment:

```ini
# IDE0051: Remove unused private members
# Suppressed because Marten uses reflection to discover Apply() methods
dotnet_diagnostic.IDE0051.severity = none
```

This is cleaner than `#pragma warning disable` scattered through source files.
