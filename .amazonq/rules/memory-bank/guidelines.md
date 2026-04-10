# DocTracking - Development Guidelines

## Code Quality Standards

### Naming Conventions
- Classes, methods, properties: PascalCase (`DocumentQueryService`, `GetAllDocumentsAsync`)
- Private fields: underscore prefix camelCase (`_context`, `_hub`, `_logger`)
- Local variables and parameters: camelCase (`officeId`, `searchString`)
- Constants: PascalCase or ALL_CAPS for static readonly arrays (`AllowedExtensions`, `PageSize`)
- Blazor component files: PascalCase matching the route concept (`ManageOffice.razor`, `CreateDocument.razor`)

### Nullable Reference Types
- Nullable is enabled project-wide (`<Nullable>enable</Nullable>`)
- Navigation properties on models are nullable: `Office? CurrentOffice`
- String properties that may be absent are nullable: `string? Description`
- Non-nullable strings use default values: `string Status { get; set; } = "In Progress"`
- Always null-check before use: `appUser?.IsOfficeHead == true`

### Async Patterns
- All I/O methods are async and return `Task` or `Task<T>`
- Method names end with `Async` suffix
- Use `await Task.WhenAll(task1, task2)` for parallel independent calls
- Use `IAsyncEnumerable<T>` + `.AsAsyncEnumerable()` for streaming large datasets (CSV export)
- `async void` is only used in `DebounceHelper.Trigger` (fire-and-forget debounce pattern)

### Error Handling
- All service methods wrap logic in `try/catch` and return safe defaults on failure
- Server-side: log with `_logger.LogError(ex, "[MethodName] Failed for {Param}", value)`
- Client-side: log with `Console.WriteLine($"[ClassName] MethodName failed: {ex.Message}")`
- Log tag format: `[ClassName]` prefix in brackets for easy filtering
- Controllers return `StatusCode(500, "message")` for unexpected errors
- Service methods return `(false, errorMessage)` tuples rather than throwing to callers

### Return Patterns
- Mutation operations return `(bool Success, string? Error)` tuples
- Create operations return `(bool Success, string? Error, T? Created)` tuples
- Query operations return `(List<T> Items, int TotalCount)` for paginated results
- Failed queries return empty collections, never null: `return (new List<Document>(), 0)`
- Client service methods return `?? new()` as fallback: `await GetJsonAsync<List<T>>(url) ?? new()`

---

## Structural Conventions

### Controller Pattern
- All API controllers inherit `ControllerBase` (not `Controller`)
- Class-level attributes: `[Route("api/[controller]")]`, `[ApiController]`, `[Authorize]`, `[EnableRateLimiting("api")]`
- Current user resolved via property: `string? CurrentUserEmail => User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email)`
- Complex queries delegated to `DocumentQueryService`, not inline in controllers
- Notification side effects handled via private `NotifyGroup` / `NotifyOfficeOrUnit` helpers
- Notifications queued in a `List<AppNotification>` then bulk-saved via `SaveNotifications`

```csharp
// Standard controller action pattern
[HttpGet("some-endpoint")]
[Authorize(Roles = "Admin")]
public async Task<ActionResult<PagedResult<T>>> GetSomething([FromQuery] int page = 1)
{
    var (items, total) = await _service.GetSomethingAsync(page);
    return Ok(new PagedResult<T> { Items = items, TotalCount = total });
}
```

### Service Layer (DocumentQueryService)
- Private filter helpers: `ApplyDocumentFilters`, `ApplyAuditFilters` — reused across multiple public methods
- Private ordering helper: `OrderByPriority` — static, returns `IOrderedQueryable<T>`
- Priority ordering: Emergency=0, Urgent=1, Medium=2, Low=3 (numeric sort)
- All public methods follow the same try/catch/log/return-default pattern

```csharp
// Standard service method pattern
public async Task<List<Document>> GetSomethingAsync(int id)
{
    try
    {
        return await _context.Documents
            .Include(d => d.Creator)
            .Where(d => d.SomeId == id)
            .ToListAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[GetSomethingAsync] Failed for id {Id}", id);
        return new List<Document>();
    }
}
```

### Client HTTP Service (DocumentService)
- All GET calls go through private `GetJsonAsync<T>(url)` helper
- All mutation calls go through private `ToResult(Task<HttpResponseMessage>)` helper
- HTML response detection: `if (content.TrimStart().StartsWith('<')) return default` — guards against auth redirects returning HTML
- URL building: append query params conditionally with `Uri.EscapeDataString`
- `UnauthorizedAccessException` is re-thrown (not swallowed) to allow UI to redirect

```csharp
// Standard GET pattern
public async Task<SomeModel?> GetSomethingAsync(int id) =>
    await GetJsonAsync<SomeModel>($"api/something/{id}");

// Standard mutation pattern
public Task<(bool Success, string? Error)> UpdateSomethingAsync(SomeModel model) =>
    ToResult(_http.PutAsJsonAsync($"api/something/{model.Id}", model));
```

---

## Blazor Component Patterns

### Page Component Structure
Every page component follows this order:
1. `@page` directive + `@attribute [Authorize]` (with optional Roles)
2. `@using` statements
3. `@inject` services
4. `@rendermode InteractiveAuto`
5. HTML markup with MudBlazor components
6. `@code { }` block

### Render Mode
- All interactive pages use `@rendermode InteractiveAuto`
- This enables server-side rendering initially, then upgrades to WASM

### Loading States
- Boolean `loading` / `isLoadingOffices` / `isSaving` flags control UI state
- Show `MudProgressCircular` or `MudSkeleton` while loading
- Skeleton loaders mirror the actual content structure (same number of rows, same layout)
- Disable submit buttons while `isSaving`: `Disabled="@(!success || isSaving)"`

### MudBlazor Usage
- All forms use `MudForm` with `@bind-IsValid="success"`
- All inputs use `Variant="Variant.Outlined"`
- All dialogs opened via `IDialogService.ShowAsync<TDialog>(title, parameters, options)`
- Dialog result checked with pattern matching: `if (result is not { Canceled: false, Data: T data }) return;`
- Confirmations use `MudMessageBox` with `Message`, `YesText`, `CancelText` parameters
- Snackbar feedback: `Snackbar.Add("message", Severity.Success/Error/Warning/Info)`
- Responsive layout: desktop actions use `Class="d-none d-md-flex"`, mobile uses `MudMenu` with `Class="d-md-none"`
- FAB for mobile add actions: `MudFab` fixed at `bottom: 24px; right: 24px; z-index: 1000`

```razor
@* Standard dialog open pattern *@
var dialog = await DialogService.ShowAsync<MyDialog>("Title", parameters, dialogOptions);
var result = await dialog.Result;
if (result is not { Canceled: false, Data: MyType data }) return;
```

### Search & Pagination
- Search inputs use MudBlazor's built-in `DebounceInterval="400"` on `MudTextField`
- Pagination uses "Load More" pattern (append to list) rather than page numbers
- `_currentPage` tracks current page; `LoadMore` increments and appends
- `PageSize` is a `const int = 25`

### State Notification (Services)
- Services expose `event Action? OnChange`
- Mutate state then immediately call `OnChange?.Invoke()` before awaiting the API call
- Components subscribe in `OnInitializedAsync` and call `StateHasChanged()` in handler

---

## Semantic Patterns

### Document Status Values
Exactly 4 valid statuses (use these exact strings):
- `"In Progress"` — just created, not yet routed
- `"In Motion"` — routed/forwarded, in transit
- `"Received"` — accepted at destination office/unit
- `"Completed"` — fully processed

### Document Priority Values
Exactly 4 valid priorities (ordered by urgency):
- `"Emergency"` → Color.Error
- `"Urgent"` → Color.Warning
- `"Medium"` → Color.Info
- `"Low"` → Color.Success

### SignalR Group Naming
Groups follow strict naming conventions:
- `user-{userId}` — individual user
- `unit-{unitId}` — all members of a unit
- `office-{officeId}` — non-head, non-unit office staff
- `office-head-{officeId}` — office heads only

### Claims & Authorization
- Role claim: `ClaimTypes.Role` + `"roles"` (both added for compatibility)
- Custom claims added by `UserClaimsTransformation`: `"UnitId"`, `"OfficeId"`, `"IsOfficeHead"`
- Claims cached in `IMemoryCache` for 5 minutes per user email
- Cache invalidated via `UserClaimsTransformation.InvalidateUser(email)` after user updates

### EF Core Conventions
- Always use `.Include()` for navigation properties needed in the result
- Use composite indexes for frequently filtered combinations (Status+OfficeId, UserId+IsRead)
- `DeleteBehavior.Restrict` on most FK relationships; `DeleteBehavior.Cascade` only for `AppNotification → AppUser`
- Timestamps stored as `DateTime.UtcNow`; convert to local time only at display layer

### DocHelpers Static Class
All document display logic (colors, icons, labels) is centralized in `DocHelpers`:
- `GetIconForAction(action)` → MudBlazor icon string
- `GetActionColor(action)` → MudBlazor `Color` enum
- `GetPriorityColor(priority)` → MudBlazor `Color` enum
- `GetStatusColor(status)` → MudBlazor `Color` enum
- Always use these helpers in components — never hardcode colors/icons for document states

### DebounceHelper Usage
```csharp
// In component @code block
private readonly DebounceHelper _debounce = new(400);

private void OnSearchInput(string value)
{
    searchString = value;
    _debounce.Trigger(async () =>
    {
        await LoadData();
        await InvokeAsync(StateHasChanged);
    });
}

public void Dispose() => _debounce.Dispose();
```

### File Upload Pattern
1. Validate extension against `AllowedExtensions` array before accepting
2. Upload via `DocService.UploadFileAsync(file)` → returns path string
3. On document save failure, clean up orphaned file via `DocService.DeleteUploadAsync(path)`
4. Max file size: 10 MB (`maxAllowedSize: 10 * 1024 * 1024`)
5. Accepted types: `.pdf`, `.doc`, `.docx`, `.jpg`, `.png`
