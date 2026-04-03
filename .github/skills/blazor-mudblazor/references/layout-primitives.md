# Layout Primitives, Typography, and Icons

## MudContainer

Constrains content to a max-width with auto horizontal padding. Use at the page level.

```razor
<MudContainer MaxWidth="MaxWidth.Large" Class="mt-8 mb-12">
    @* page content *@
</MudContainer>
```

| `MaxWidth` value | Width |
|---|---|
| `ExtraSmall` | 444px |
| `Small` | 600px |
| `Medium` | 900px |
| `Large` | 1200px |
| `ExtraLarge` | 1536px |
| `ExtraExtraLarge` | 2560px |
| `False` | 100% (no max) |

---

## MudGrid / MudItem — Responsive 12-Column Layout

```razor
<MudGrid Spacing="3">
    <MudItem xs="12" sm="8">
        <MudTextField T="string" Label="Title" @bind-Value="_model.Title"/>
    </MudItem>
    <MudItem xs="12" sm="4">
        <MudTextField T="string" Label="ISBN" @bind-Value="_model.Isbn"/>
    </MudItem>
</MudGrid>
```

`xs`/`sm`/`md`/`lg`/`xl` map to breakpoint column spans (1–12). Use `xs="12"` as the mobile default and narrow with `sm`/`md` for wider viewports.

---

## MudStack — Flexbox Stack

Simpler than MudGrid when you don't need the 12-column system.

```razor
@* Horizontal row with spacing *@
<MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
    <MudText Typo="Typo.h4">Title</MudText>
    <MudSpacer/>
    <MudButton>Add</MudButton>
</MudStack>

@* Vertical stack (default) *@
<MudStack Spacing="3">
    <MudTextField .../>
    <MudTextField .../>
    <MudButton .../>
</MudStack>
```

| Prop | Values |
|---|---|
| `Row` | `true` = horizontal, `false` (default) = vertical |
| `AlignItems` | `AlignItems.Center / Start / End / Baseline / Stretch` |
| `Justify` | `Justify.FlexStart / Center / FlexEnd / SpaceBetween / SpaceAround / SpaceEvenly` |
| `Spacing` | 0–16 (MudBlazor spacing scale) |

---

## MudPaper — Elevated Surface

```razor
<MudPaper Elevation="2" Class="pa-4" Style="border-radius: 12px;">
    @* card content *@
</MudPaper>
```

`Elevation` 0–25; use 0 for a flat surface with a border, 2–4 for cards, 8+ for floating elements.

---

## MudCard

Use `MudCard` when you want a semantic card with optional header/media/content/actions:

```razor
<MudCard Elevation="2">
    <MudCardHeader>
        <CardHeaderContent>
            <MudText Typo="Typo.h6">Widget Details</MudText>
        </CardHeaderContent>
    </MudCardHeader>
    <MudCardContent>
        @* content *@
    </MudCardContent>
    <MudCardActions>
        <MudButton Color="Color.Primary" Variant="Variant.Filled">Save</MudButton>
    </MudCardActions>
</MudCard>
```

---

## MudText — Typography

Always use `MudText` with `Typo` in MudBlazor layouts — not raw `<h1>`–`<h6>` or `<p>`.

```razor
<MudText Typo="Typo.h3">Page Title</MudText>
<MudText Typo="Typo.h4" Class="fw-bold">Section Title</MudText>
<MudText Typo="Typo.h6">Card Title</MudText>
<MudText Typo="Typo.subtitle1">Subtitle</MudText>
<MudText Typo="Typo.subtitle2" Color="Color.Secondary">Caption-style</MudText>
<MudText Typo="Typo.body1">Body paragraph</MudText>
<MudText Typo="Typo.body2">Dense body text</MudText>
<MudText Typo="Typo.caption">Caption text</MudText>
```

`Color` accepts `Color.Primary`, `Color.Secondary`, `Color.Error`, `Color.Success`, `Color.Warning`, `Color.Info`, `Color.Inherit`.

---

## MudDivider

```razor
<MudDivider Class="my-4"/>
<MudDivider Vertical="true" FlexItem="true"/>   @* vertical, inside a flex row *@
```

---

## MudSpacer

Fills remaining horizontal (or vertical) space in a flex container, pushing siblings apart:

```razor
<MudStack Row="true">
    <MudText Typo="Typo.h4">Title</MudText>
    <MudSpacer/>
    <MudButton>Action</MudButton>
</MudStack>
```

---

## Icons

All icons are static string constants. The three variants:

```razor
Icons.Material.Filled.Add
Icons.Material.Outlined.Edit
Icons.Material.TwoTone.Delete
```

Use in components:

```razor
<MudIcon Icon="@Icons.Material.Filled.Book"/>
<MudIconButton Icon="@Icons.Material.Filled.Edit" Color="Color.Primary" Size="Size.Small" OnClick="HandleClick"/>
<MudButton StartIcon="@Icons.Material.Filled.Add" Variant="Variant.Filled" Color="Color.Primary">Add Item</MudButton>
```

Common icons:
- Add / Remove: `Add`, `Remove`, `AddCircle`
- Edit / Delete: `Edit`, `Delete`, `DeleteOutline`
- Navigation: `Menu`, `ArrowBack`, `ChevronRight`
- Data: `Search`, `FilterList`, `Sort`
- Status: `CheckCircle`, `Error`, `Warning`, `Info`
- User: `Person`, `People`, `Lock`, `LockOpen`
- Business:  `Book`, `MenuBook`, `Business`, `Store`
- Theme: `LightMode`, `DarkMode`, `SettingsBrightness`

---

## MudTooltip

```razor
<MudTooltip Text="Add new item" Arrow="true" Placement="Placement.Bottom">
    <MudIconButton Icon="@Icons.Material.Filled.Add" Color="Color.Primary"/>
</MudTooltip>
```

---

## MudList / MudListItem

Use for simple vertical lists (e.g., translation entries in a dialog):

```razor
<MudList T="string" Dense="true">
    @foreach (var item in _items)
    {
        <MudListItem Icon="@Icons.Material.Filled.Language">
            <div class="d-flex justify-space-between align-center" style="width:100%">
                <MudText>@item.Label</MudText>
                <MudIconButton Icon="@Icons.Material.Filled.Delete"
                               Color="Color.Error" Size="Size.Small"
                               OnClick="@(() => Remove(item))"/>
            </div>
        </MudListItem>
    }
</MudList>
```

---

## Utility CSS classes (MudBlazor built-in)

| Class | Effect |
|---|---|
| `pa-4`, `px-2`, `py-6` | Padding (all / horizontal / vertical), scale 0–16 |
| `ma-4`, `mx-auto`, `mb-4` | Margin |
| `mt-8 mb-12` | Top/bottom margin (standard page content padding) |
| `d-flex`, `flex-grow-1` | Flexbox shorthands |
| `justify-space-between`, `align-center` | Flex alignment |
| `gap-2`, `gap-3` | Flex gap |
| `rounded-xl` | Large border radius |
| `overflow-hidden` | Clip overflow |
| `fw-bold`, `fw-semibold` | Font weight (custom, add to your CSS) |
