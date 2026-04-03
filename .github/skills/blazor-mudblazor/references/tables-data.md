# MudTable — Server-Side Data

## Standard Server-Side Table with Search

Admin list pages use `MudTable<T>` with `ServerData` for server-side paging, filtering, and sorting. The search string uses a property (not a field) to auto-trigger `ReloadServerData()`.

```razor
<MudTable T="AdminWidgetDto"
          @ref="_table"
          ServerData="ReloadData"
          Hover="true"
          Elevation="0"
          Breakpoint="Breakpoint.Sm"
          Class="glass-table rounded-xl overflow-hidden">
    <ToolBarContent>
        <MudSpacer/>
        <MudTextField @bind-Value="_searchString"
                      Placeholder="Search widgets..."
                      Adornment="Adornment.Start"
                      AdornmentIcon="@Icons.Material.Filled.Search"
                      Clearable="true"
                      Immediate="true"
                      Variant="Variant.Outlined"
                      Margin="Margin.Dense"/>
    </ToolBarContent>
    <HeaderContent>
        <MudTh>
            <MudTableSortLabel SortLabel="name" T="AdminWidgetDto">Name</MudTableSortLabel>
        </MudTh>
        <MudTh>Status</MudTh>
        <MudTh>Actions</MudTh>
    </HeaderContent>
    <RowTemplate>
        <MudTd DataLabel="Name">
            <MudText Typo="Typo.body2">@context.Name</MudText>
        </MudTd>
        <MudTd DataLabel="Status">
            <MudChip T="string" Size="Size.Small"
                     Color="@(context.IsActive ? Color.Success : Color.Error)"
                     Variant="Variant.Text">
                @(context.IsActive ? "Active" : "Inactive")
            </MudChip>
        </MudTd>
        <MudTd DataLabel="Actions">
            <MudMenu Icon="@Icons.Material.Filled.MoreVert"
                     AnchorOrigin="Origin.BottomRight"
                     TransformOrigin="Origin.TopRight"
                     Dense="true">
                <MudMenuItem Icon="@Icons.Material.Filled.Edit"
                             OnClick="@(() => EditWidget(context))">Edit</MudMenuItem>
                <MudMenuItem Icon="@Icons.Material.Filled.Delete"
                             OnClick="@(() => DeleteWidget(context.Id, context.ETag))">Delete</MudMenuItem>
            </MudMenu>
        </MudTd>
    </RowTemplate>
    <NoRecordsContent><MudText>No widgets found.</MudText></NoRecordsContent>
    <LoadingContent><MudText>Loading...</MudText></LoadingContent>
    <PagerContent><MudTablePager/></PagerContent>
</MudTable>
```

```csharp
@code {
    private MudTable<AdminWidgetDto> _table = null!;

    // Use a property — the setter fires ReloadServerData() automatically
    private string? _searchString
    {
        get  => _searchStringValue;
        set  {
            if (_searchStringValue == value) return;
            _searchStringValue = value;
            _table.ReloadServerData();   // MudTable handles the returned Task
        }
    }
    private string? _searchStringValue;

    private async Task<TableData<AdminWidgetDto>> ReloadData(TableState state, CancellationToken ct)
    {
        var result = await WidgetsClient.GetWidgetsAsync(
            page:     state.Page,
            pageSize: state.PageSize,
            search:   _searchStringValue,
            sortBy:   state.SortLabel,
            sortDesc: state.SortDirection == SortDirection.Descending,
            ct);

        return new TableData<AdminWidgetDto>
        {
            Items      = result.Items,
            TotalItems = result.TotalCount
        };
    }
}
```

### Reload after mutation

After create/edit/delete dialogs close successfully, reload the table:

```csharp
var dialog = await DialogService.ShowAsync<CreateWidgetDialog>("New Widget", options);
var result = await dialog.Result;
if (result is { Canceled: false })
    await _table.ReloadServerData();
```

### SSE-driven auto-reload

```csharp
private async void HandleNotification(IDomainEventNotification notification)
{
    if (InvalidationService.ShouldInvalidate(notification, ["Widgets"]))
        await InvokeAsync(_table.ReloadServerData);
}
```

---

## MudDataGrid (alternative for complex grids)

Use `MudDataGrid` when you need built-in filtering columns or cell editing. For most admin CRUD pages `MudTable` is simpler.

```razor
<MudDataGrid T="WidgetDto" Items="@_widgets" Dense="true" Hover="true" Striped="true">
    <Columns>
        <PropertyColumn Property="x => x.Name" />
        <PropertyColumn Property="x => x.Price" Format="F2" Title="Price (USD)" />
        <TemplateColumn Title="Actions">
            <CellTemplate>
                <MudIconButton Icon="@Icons.Material.Filled.Edit"
                               OnClick="@(() => Edit(context.Item))" Size="Size.Small"/>
            </CellTemplate>
        </TemplateColumn>
    </Columns>
</MudDataGrid>
```

---

## Table Styling Conventions (BookStore)

The project's glass morphism table style:

```razor
Class="glass-table rounded-xl overflow-hidden"
```

```css
.glass-table {
    background: rgba(255,255,255,0.7) !important;
    backdrop-filter: blur(10px);
    border: 1px solid rgba(255,255,255,0.3);
    box-shadow: 0 8px 32px 0 rgba(31,38,135,0.07) !important;
}
```
