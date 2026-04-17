---
name: blazor-mudblazor
description: Use MudBlazor components correctly in Blazor applications — covering setup (providers, theme, dark-mode), layout shell (MudLayout/MudAppBar/MudDrawer), data tables (MudTable with ServerData, server-side paging, search), forms and inputs (MudForm, MudTextField, MudAutocomplete, MudSelect, MudNumericField, MudChipSet), dialogs (MudDialog, IDialogService, IMudDialogInstance), feedback (ISnackbar, MudAlert, MudSkeleton, MudProgressCircular), navigation menus (MudMenu, MudIconButton), and layout primitives (MudGrid, MudStack, MudPaper, MudContainer, MudText, Icons.Material). Trigger whenever the user writes, reviews, or asks about MudBlazor components, MudForm validation, MudTable server-side data, MudDialog patterns, MudAutocomplete, theming, dark mode, snackbar notifications, Material icons, or any UI component work in a Blazor project using MudBlazor — even if they don't explicitly say "MudBlazor". Always prefer this skill over guessing; component APIs, the IMudDialogInstance cascading parameter, and the IsDarkMode wiring have non-obvious failure modes.
---

# MudBlazor — Component Patterns

MudBlazor is the UI component library for BookStore's Blazor frontend. Components follow Material Design and are built for interactive Blazor Server (`@rendermode InteractiveServer`).

## Quick Reference

| Topic | Reference |
|---|---|
| Providers, theme, dark mode, MudLayout shell | `references/setup-layout.md` |
| MudTable (server-side paging, search, sort) | `references/tables-data.md` |
| MudForm, inputs, MudAutocomplete, MudSelect | `references/forms-inputs.md` |
| MudDialog open/close/parameters, ISnackbar, MudAlert | `references/dialogs-feedback.md` |
| MudGrid, MudStack, MudPaper, MudText, Icons | `references/layout-primitives.md` |

Related skills: `../csharp-blazor/SKILL.md` (ReactiveQuery, SSE subscriptions, full page skeleton), `../bunit/SKILL.md` (testing), `../etag/SKILL.md` (optimistic concurrency in dialogs).

---

## Essential Rules

- `MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider` go in `Routes.razor` (root), **not** in `MainLayout.razor`.
- Dialog components do **not** declare `@rendermode` — they inherit it from the parent page.
- Dialog code-behind uses `[CascadingParameter] IMudDialogInstance MudDialog { get; set; } = null!;` — **not** `MudDialogInstance`.
- Always pass `DialogParameters<TDialog>` with a typed lambda: `{ x => x.MyParam, value }` — this is compile-time safe.
- `MudForm` owns validation state (`@bind-IsValid`). Don't mix with `EditContext`/`DataAnnotations` unless you need both.
- All text components use `MudText` with a `Typo` enum value — never raw HTML `<h1>`/`<p>` tags inside MudBlazor layouts.
- Icons come from `Icons.Material.Filled.*`, `Icons.Material.Outlined.*`, or `Icons.Material.TwoTone.*`.

---

## Minimal Admin Page Pattern

Every admin management page follows this shell (details in `../csharp-blazor/SKILL.md`):

```razor
<MudContainer MaxWidth="MaxWidth.Large" Class="mt-8 mb-12">
    <MudStack Row="true" AlignItems="AlignItems.Center" Class="mb-6">
        <MudText Typo="Typo.h4">Widget Management</MudText>
        <MudSpacer/>
        <MudButton Variant="Variant.Filled" Color="Color.Primary"
                   StartIcon="@Icons.Material.Filled.Add"
                   OnClick="OpenAddDialog">
            Add Widget
        </MudButton>
    </MudStack>

    @* MudTable or MudDataGrid here — see references/tables-data.md *@
</MudContainer>
```

---

## Common Gotchas

- `MudForm` validation: call `await _form.ValidateAsync()` (not `Validate()` — removed in v9).
- `IDialogService` confirmation: use `await DialogService.ShowMessageBoxAsync(...)` (renamed from `ShowMessageBox` in v9).
- `MudTable<T>` search: use a **property** with a setter that calls `_table.ReloadServerData()`, not field + `@bind-Value`. See `references/tables-data.md`.
- `MudTabs.PanelClass` was removed in v9 — set `PanelClass` on each `<MudTabPanel>` individually instead.
- `MudAutocomplete<T>` needs both `SearchFunc` and `ToStringFunc` for object types.
- `ISnackbar.Add(message, Severity.X)` — inject `ISnackbar Snackbar` via `@inject`.
- `MudDialog` requires `MudDialogProvider` to be registered in `Routes.razor`.
- Closing a dialog: `MudDialog.Close(DialogResult.Ok(true))` or `MudDialog.Cancel()`.
- `MudChipSet<T>` — bind chips via `Value` and `Text`; use `OnClose` to remove an item from a collection.
