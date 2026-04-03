# Forms, Tables, Dialogs

## MudForm — The Standard Form Pattern

BookStore uses MudBlazor's own form validation (`MudForm`), **not** `EditContext` or `DataAnnotations`. This keeps validation logic in the component rather than spread across model attributes.

```razor
<MudForm @ref="_form" @bind-IsValid="_success">
    <MudTextField T="string"
                  Label="Name"
                  @bind-Value="_model.Name"
                  Required="true"
                  RequiredError="Name is required!" />

    <MudTextField T="string"
                  Label="Email"
                  @bind-Value="_model.Email"
                  InputType="InputType.Email"
                  Required="true"
                  RequiredError="Valid email required!" />

    <MudButton OnClick="Submit"
               Variant="Variant.Filled"
               Color="Color.Primary"
               Disabled="@(!_success)">Save</MudButton>
</MudForm>

@code {
    MudForm _form = null!;
    bool _success;

    record CreateWidgetRequest(string Name, string Email);
    CreateWidgetRequest _model = new("", "");

    async Task Submit()
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

---

## MudTable — Server-Side Paging

Admin management pages use `MudTable` with `ServerData` for server-side paging and filtering.

```razor
<MudTable T="WidgetDto"
          @ref="_table"
          ServerData="ReloadData"
          Dense="true"
          Bordered="true">
    <ToolBarContent>
        <MudText Typo="Typo.h6">Widgets</MudText>
        <MudSpacer />
        <MudTextField @bind-Value="SearchString"
                      Placeholder="Search..."
                      Adornment="Adornment.Start"
                      AdornmentIcon="@Icons.Material.Filled.Search"
                      Immediate="true" />
    </ToolBarContent>
    <HeaderContent>
        <MudTh>Name</MudTh>
        <MudTh>Actions</MudTh>
    </HeaderContent>
    <RowTemplate>
        <MudTd>@context.Name</MudTd>
        <MudTd>
            <MudMenuItem OnClick="@(() => Edit(context))">Edit</MudMenuItem>
            <MudMenuItem OnClick="@(() => Delete(context.Id, context.ETag))">Delete</MudMenuItem>
        </MudTd>
    </RowTemplate>
    <PagerContent>
        <MudTablePager />
    </PagerContent>
</MudTable>
```

### Auto-Reload on Search Change

Use a backing field with a property setter to trigger `ReloadServerData()` automatically when the search string changes:

```csharp
private string? _searchStringValue;
private MudTable<WidgetDto> _table = null!;

private string? SearchString
{
    get => _searchStringValue;
    set
    {
        if (_searchStringValue == value) return;
        _searchStringValue = value;
        _table.ReloadServerData();   // fire-and-forget; MudTable handles the Task
    }
}

private async Task<TableData<WidgetDto>> ReloadData(TableState state, CancellationToken ct)
{
    var result = await WidgetsClient.GetWidgetsAsync(
        page: state.Page,
        pageSize: state.PageSize,
        search: _searchStringValue,
        ct);

    return new TableData<WidgetDto>
    {
        Items = result.Items,
        TotalItems = result.TotalCount
    };
}
```

---

## Dialogs — Open and Close Pattern

### Opening a Dialog (parent side)

```csharp
@inject IDialogService DialogService

private async Task OpenCreateDialog()
{
    var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small };
    var dialog = await DialogService.ShowAsync<CreateWidgetDialog>("New Widget", options);
    var result = await dialog.Result;

    if (result != null && !result.Canceled)
    {
        // Dialog confirmed — reload data
        await _table.ReloadServerData();
    }
}
```

### Passing Parameters to a Dialog

```csharp
var parameters = new DialogParameters<UpdateWidgetDialog>
{
    { x => x.Widget, selectedWidget },
    { x => x.ETag,   selectedWidget.ETag }
};
var dialog = await DialogService.ShowAsync<UpdateWidgetDialog>("Edit Widget", parameters, options);
```

### Dialog Component

```razor
@* CreateWidgetDialog.razor — NO @rendermode, inherits from parent *@

<MudDialog>
    <TitleContent>
        <MudText Typo="Typo.h6">New Widget</MudText>
    </TitleContent>
    <DialogContent>
        <MudForm @ref="_form" @bind-IsValid="_success">
            <MudTextField T="string" Label="Name" @bind-Value="_model.Name"
                          Required="true" RequiredError="Name is required!" />
        </MudForm>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="Color.Primary" OnClick="Submit" Disabled="@(!_success)">Create</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public CreateWidgetRequest? InitialModel { get; set; }

    MudForm _form = null!;
    bool _success;
    CreateWidgetRequest _model = new("");

    protected override void OnParametersSet()
    {
        if (InitialModel != null) _model = InitialModel;
    }

    void Cancel() => MudDialog.Cancel();

    async Task Submit()
    {
        await _form.Validate();
        if (!_success) return;

        var result = await WidgetsClient.CreateWidgetAsync(_model);
        if (result.IsSuccess)
            MudDialog.Close(DialogResult.Ok(true));
        else
            Snackbar.Add(result.Error.Message, Severity.Error);
    }
}
```

---

## ETags for Optimistic Concurrency

Admin pages pass ETags from the `AdminWidgetDto` to edit/delete operations to prevent lost updates:

```razor
<MudMenuItem OnClick="@(() => DeleteWidget(context.Id, context.ETag))">Delete</MudMenuItem>
```

```csharp
private async Task DeleteWidget(Guid id, string etag)
{
    var result = await WidgetsClient.DeleteWidgetAsync(id, etag);
    if (result.IsSuccess)
        Snackbar.Add("Deleted.", Severity.Success);
    else if (result.Error.Code == ErrorCode.ConcurrencyConflict)
        Snackbar.Add("Another user modified this item. Refreshing.", Severity.Warning);
    else
        Snackbar.Add(result.Error.Message, Severity.Error);

    await _table.ReloadServerData();
}
```

See the `../etag/SKILL.md` skill for the backend ETag implementation.

---

## Search Debounce Pattern

For search inputs that trigger API calls on each keystroke, use a `System.Threading.Timer` to debounce:

```csharp
System.Threading.Timer? _debounceTimer;

private void OnSearchKeyUp(KeyboardEventArgs e)
{
    if (e.Key == "Enter")
    {
        _ = SearchAsync();
        return;
    }

    _debounceTimer?.Dispose();
    _debounceTimer = new System.Threading.Timer(
        async _ => await InvokeAsync(SearchAsync),
        null,
        UIConstants.DebounceDelay,   // e.g., 300ms
        Timeout.Infinite);
}

// Dispose the timer in Dispose():
public void Dispose()
{
    // ...
    _debounceTimer?.Dispose();
}
```
