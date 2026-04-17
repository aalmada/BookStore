# Dialogs and User Feedback

## Opening a Dialog (parent / page side)

```csharp
@inject IDialogService DialogService

private async Task OpenAddDialog()
{
    var options = new DialogOptions
    {
        CloseOnEscapeKey = true,
        MaxWidth = MaxWidth.Small,
        FullWidth = true
    };
    var dialog = await DialogService.ShowAsync<CreateWidgetDialog>("New Widget", options);
    var result = await dialog.Result;

    if (result is { Canceled: false })
        await _table.ReloadServerData();
}
```

### Passing Parameters to a Dialog

Use the strongly-typed `DialogParameters<TDialog>` overload — compile-time safe:

```csharp
private async Task EditWidget(AdminWidgetDto widget)
{
    var parameters = new DialogParameters<UpdateWidgetDialog>
    {
        { x => x.Widget, widget },
        { x => x.ETag,   widget.ETag }
    };
    var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
    var dialog  = await DialogService.ShowAsync<UpdateWidgetDialog>("Edit Widget", parameters, options);
    var result  = await dialog.Result;

    if (result is { Canceled: false })
        await _table.ReloadServerData();
}
```

---

## Dialog Component

Dialog components **do not** declare `@rendermode` — they inherit it from their parent page. The cascading parameter is `IMudDialogInstance` (not `MudDialogInstance`).

```razor
@* CreateWidgetDialog.razor — NO @rendermode directive *@
@inject IWidgetsClient WidgetsClient
@inject ISnackbar Snackbar

<MudDialog>
    <TitleContent>
        <MudText Typo="Typo.h6">
            <MudIcon Icon="@Icons.Material.Filled.Widgets" Class="mr-3 mb-n1"/>
            New Widget
        </MudText>
    </TitleContent>
    <DialogContent>
        <MudForm @ref="_form" @bind-IsValid="_success">
            <MudTextField T="string"
                          Label="Name"
                          @bind-Value="_model.Name"
                          Required="true"
                          RequiredError="Name is required!"
                          Variant="Variant.Outlined"
                          Immediate="true"/>
        </MudForm>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="Color.Primary"
                   Variant="Variant.Filled"
                   OnClick="Submit"
                   Disabled="@(!_success)">Create</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = null!;

    private MudForm _form = null!;
    private bool _success;
    private CreateWidgetRequest _model = new("");

    private void Cancel() => MudDialog.Cancel();

    private async Task Submit()
    {
        await _form.ValidateAsync();
        if (!_success) return;

        var result = await WidgetsClient.CreateWidgetAsync(_model);
        if (result.IsSuccess)
            MudDialog.Close(DialogResult.Ok(true));
        else
            Snackbar.Add(result.Error.Message, Severity.Error);
    }
}
```

### Dialog with pre-populated model (edit)

```razor
@code {
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public AdminWidgetDto Widget { get; set; } = null!;
    [Parameter] public string ETag { get; set; } = "";

    protected override void OnParametersSet()
    {
        _model = new UpdateWidgetRequest(Widget.Name, Widget.Description);
    }
}
```

---

## Confirmation Dialog

For destructive actions, use `IDialogService` with a simple confirm dialog:

```csharp
private async Task DeleteWidget(Guid id, string etag)
{
    var confirm = await DialogService.ShowMessageBoxAsync(
        "Delete Widget",
        "Are you sure you want to delete this widget? This cannot be undone.",
        yesText: "Delete", cancelText: "Cancel");

    if (confirm != true) return;

    var result = await WidgetsClient.DeleteWidgetAsync(id, etag);
    if (result.IsSuccess)
        Snackbar.Add("Widget deleted.", Severity.Success);
    else if (result.Error.Code == ErrorCode.ConcurrencyConflict)
        Snackbar.Add("Another user modified this item. Please refresh.", Severity.Warning);
    else
        Snackbar.Add(result.Error.Message, Severity.Error);

    await _table.ReloadServerData();
}
```

---

## ISnackbar — Toast Notifications

Inject once per component: `@inject ISnackbar Snackbar`

```csharp
Snackbar.Add("Operation successful!",             Severity.Success);
Snackbar.Add("Something went wrong.",             Severity.Error);
Snackbar.Add("Check your input.",                 Severity.Warning);
Snackbar.Add("Refreshing in background...",       Severity.Info);
```

---

## MudAlert — Inline Alerts

```razor
<MudAlert Severity="Severity.Warning" Dense="true">
    At least one author is required.
</MudAlert>

<MudAlert Severity="Severity.Error" Variant="Variant.Filled">
    @_errorMessage
</MudAlert>
```

Use `Dense="true"` inside dialogs and cards; `Variant="Variant.Filled"` for high-emphasis alerts.

---

## MudSkeleton — Loading States

Show while initial data is loading (before `ReactiveQuery<T>` resolves):

```razor
@if (_query?.IsLoading == true && _query.Data == null)
{
    <MudStack>
        <MudSkeleton SkeletonType="SkeletonType.Rectangle" Height="40px"/>
        <MudSkeleton Width="60%"/>
        <MudSkeleton Width="80%"/>
    </MudStack>
}
```

---

## MudProgressCircular — Spinner

Inline spinner (e.g., in AppBar while tenant loads):

```razor
@if (TenantService.IsLoading)
{
    <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="mr-2"/>
}
```
