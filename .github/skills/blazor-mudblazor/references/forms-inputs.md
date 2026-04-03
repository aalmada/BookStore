# Forms and Input Components

## MudForm — Standard Pattern

BookStore uses `MudForm` for validation, **not** `EditContext` or `DataAnnotations` on models (unless complex cross-field validation is needed). The form gate: `Disabled="@(!_success)"` on the submit button.

```razor
<MudForm @ref="_form" @bind-IsValid="_success">
    <MudTextField T="string"
                  Label="Name"
                  @bind-Value="_model.Name"
                  Required="true"
                  RequiredError="Name is required!"
                  Variant="Variant.Outlined"
                  Immediate="true"/>

    <MudNumericField T="decimal"
                     Label="Price"
                     @bind-Value="_model.Price"
                     Required="true"
                     Min="0"
                     Variant="Variant.Outlined"/>

    <MudButton Color="Color.Primary"
               Variant="Variant.Filled"
               OnClick="Submit"
               Disabled="@(!_success)">
        Save
    </MudButton>
</MudForm>

@code {
    private MudForm _form = null!;
    private bool _success;
    private CreateWidgetRequest _model = new("", 0m);

    private async Task Submit()
    {
        await _form.Validate();
        if (!_success) return;

        var result = await WidgetsClient.CreateWidgetAsync(_model);
        if (result.IsSuccess)
            Snackbar.Add("Widget created!", Severity.Success);
        else
            Snackbar.Add(result.Error.Message, Severity.Error);
    }
}
```

### Async / custom validation

```razor
<MudTextField T="string"
              Label="Slug"
              @bind-Value="_model.Slug"
              Validation="@(new Func<string, Task<string?>>(ValidateSlugAsync))" />

@code {
    private async Task<string?> ValidateSlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return "Slug is required";
        var exists = await WidgetsClient.SlugExistsAsync(slug);
        return exists ? "Slug already taken" : null;
    }
}
```

---

## MudTextField variants

```razor
@* Password *@
<MudTextField T="string" InputType="InputType.Password" Label="Password" @bind-Value="_password"/>

@* Multiline *@
<MudTextField T="string" Lines="3" Label="Notes" @bind-Value="_notes" Variant="Variant.Outlined"/>

@* With adornment *@
<MudTextField T="string" Label="Price"
              Adornment="Adornment.Start" AdornmentText="$"
              @bind-Value="_price" Variant="Variant.Outlined"/>

@* Immediate updates (fires on every keystroke) *@
<MudTextField T="string" Immediate="true" @bind-Value="_search"/>
```

---

## MudAutocomplete — Object binding

Use when users search-and-select from a remote list (authors, publishers, categories).

```razor
<MudAutocomplete T="PublisherDto"
                 Label="Publisher"
                 @bind-Value="_selectedPublisher"
                 SearchFunc="@SearchPublishers"
                 ToStringFunc="@(p => p?.Name ?? string.Empty)"
                 Variant="Variant.Outlined"
                 Clearable="true"
                 ResetValueOnEmptyText="true"
                 CoerceText="true"
                 CoerceValue="true"/>

@code {
    private PublisherDto? _selectedPublisher;

    private async Task<IEnumerable<PublisherDto>> SearchPublishers(string value, CancellationToken ct)
    {
        var result = await PublishersClient.SearchAsync(value, ct);
        return result.Items;
    }
}
```

**Always set `ToStringFunc`** for object types — without it the autocomplete renders the type name.

Custom item template:

```razor
<MudAutocomplete T="AuthorDto" SearchFunc="@SearchAuthors" ToStringFunc="@(a => a?.Name)">
    <ItemTemplate>
        <MudText>@context.Name</MudText>
        <MudText Typo="Typo.caption" Color="Color.Secondary">@context.Country</MudText>
    </ItemTemplate>
</MudAutocomplete>
```

---

## MudSelect

```razor
<MudSelect T="string"
           Label="Language"
           @bind-Value="_model.Language"
           Variant="Variant.Outlined"
           AnchorOrigin="Origin.BottomCenter">
    <MudSelectItem Value="@("en")">English</MudSelectItem>
    <MudSelectItem Value="@("pt")">Portuguese</MudSelectItem>
    <MudSelectItem Value="@("fr")">French</MudSelectItem>
</MudSelect>
```

Programmatic value change (fires ValueChanged only when selector triggers it):

```razor
<MudSelect T="string" Value="@sortBy" ValueChanged="@OnSortChanged" ...>
```

---

## MudChipSet — Multi-select collections

Pattern used for authors and categories on the book form:

```razor
@* Add chip *@
<MudAutocomplete T="AuthorDto" @bind-Value="_currentAuthor"
                 SearchFunc="@SearchAuthors" ToStringFunc="@(a => a?.Name)"
                 OnAdornmentClick="AddAuthorToList"
                 Adornment="Adornment.End"
                 AdornmentIcon="@Icons.Material.Filled.Add"/>

<MudChipSet T="AuthorDto" Class="mt-2">
    @foreach (var author in _selectedAuthors)
    {
        <MudChip T="AuthorDto"
                 Value="author"
                 Text="@author.Name"
                 Color="Color.Secondary"
                 Variant="Variant.Text"
                 OnClose="() => RemoveAuthor(author)"/>
    }
</MudChipSet>

@code {
    private readonly List<AuthorDto> _selectedAuthors = [];
    private AuthorDto? _currentAuthor;

    private void AddAuthorToList()
    {
        if (_currentAuthor is null || _selectedAuthors.Contains(_currentAuthor)) return;
        _selectedAuthors.Add(_currentAuthor);
        _currentAuthor = null;
    }

    private void RemoveAuthor(AuthorDto author) => _selectedAuthors.Remove(author);
}
```

---

## MudCheckBox and MudSwitch

```razor
<MudCheckBox T="bool" @bind-Value="_model.IsActive" Label="Active"/>

<MudSwitch T="bool" @bind-Value="_model.IsPublished" Color="Color.Primary">Published</MudSwitch>
```

---

## MudDatePicker

```razor
<MudDatePicker Label="Publication Date"
               @bind-Date="_publicationDate"
               Variant="Variant.Outlined"
               DateFormat="yyyy-MM-dd"/>

@code {
    private DateTime? _publicationDate;
}
```
