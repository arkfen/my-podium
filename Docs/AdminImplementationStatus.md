# Admin Dashboard Implementation - Status & Next Steps

## ? Completed

### 1. Frontend Components
- ? AdminStateService - tracks admin status and permissions
- ? AdminDashboard page - main admin portal with navigation cards
- ? DisciplineManagement page - full CRUD for disciplines
- ? AdminManagement page - manage admin privileges
- ? PlaceholderManagement page - placeholders for Series, Seasons, Competitors, Events, Results, Users
- ? ConfirmDialog component - reusable confirmation modal
- ? Admin CSS styles - modern, responsive admin interface
- ? MainLayout updated - shows admin link for admins

### 2. API Client
- ? PodiumApiClient extended with all admin methods
- ? Request DTOs for all CRUD operations
- ? PUT and DELETE helper methods

### 3. Repository Updates
- ? DisciplineRepository - added Create, Update, Delete methods
- ?? Other repositories need CRUD methods added (see below)

### 4. Backend API
- ? AdminEndpoints - season management, admin CRUD, diagnostics
- ?? Need to add CRUD endpoints for other entities (see below)

## ?? To Complete

### Repository CRUD Methods Needed

Add Create, Update, Delete methods to these repositories (following DisciplineRepository pattern):

**SeriesRepository:**
```csharp
Task<Series?> CreateSeriesAsync(Series series);
Task<Series?> UpdateSeriesAsync(Series series);
Task<bool> DeleteSeriesAsync(string seriesId);
```

**SeasonRepository:**
```csharp
Task<Season?> CreateSeasonAsync(Season season);
Task<Season?> UpdateSeasonAsync(Season season);
Task<bool> DeleteSeasonAsync(string seasonId);
```

**CompetitorRepository:**
```csharp
Task<Competitor?> CreateCompetitorAsync(Competitor competitor);
Task<Competitor?> UpdateCompetitorAsync(Competitor competitor);
Task<bool> DeleteCompetitorAsync(string competitorId);
Task<bool> AddCompetitorToSeasonAsync(string seasonId, string competitorId, string competitorName);
Task<bool> RemoveCompetitorFromSeasonAsync(string seasonId, string competitorId);
```

**EventRepository:**
```csharp
Task<Event?> CreateEventAsync(Event evt);
Task<Event?> UpdateEventAsync(Event evt);
Task<bool> DeleteEventAsync(string eventId);
```

**EventResultRepository (may need to create):**
```csharp
Task<EventResult?> CreateOrUpdateEventResultAsync(EventResult result);
Task<bool> DeleteEventResultAsync(string eventId);
```

**UserRepository:**
```csharp
Task<List<User>> GetAllUsersAsync();
Task<bool> UpdateUserAsync(User user);
Task<bool> DeleteUserAsync(string userId);
```

### API Endpoints Needed

Add to **AdminEndpoints.cs**:

```csharp
// Disciplines
group.MapGet("/disciplines", async ([FromServices] IDisciplineRepository repo) => { ... });
group.MapGet("/disciplines/{id}", async (string id, [FromServices] IDisciplineRepository repo) => { ... });
group.MapPost("/disciplines", async ([FromBody] CreateDisciplineRequest request, [FromServices] IDisciplineRepository repo) => { ... });
group.MapPut("/disciplines/{id}", async (string id, [FromBody] UpdateDisciplineRequest request, [FromServices] IDisciplineRepository repo) => { ... });
group.MapDelete("/disciplines/{id}", async (string id, [FromServices] IDisciplineRepository repo) => { ... });

// Series (similar pattern)
group.MapGet("/disciplines/{disciplineId}/series", ...);
group.MapGet("/series/{id}", ...);
group.MapPost("/series", ...);
group.MapPut("/series/{id}", ...);
group.MapDelete("/series/{id}", ...);

// Seasons (similar pattern)
group.MapGet("/series/{seriesId}/seasons", ...);
group.MapGet("/seasons/{id}", ...);
group.MapPost("/seasons", ...);
group.MapPut("/seasons/{id}", ...);
group.MapDelete("/seasons/{id}", ...);

// Competitors (similar pattern + season linking)
group.MapGet("/disciplines/{disciplineId}/competitors", ...);
group.MapGet("/competitors/{id}", ...);
group.MapPost("/competitors", ...);
group.MapPut("/competitors/{id}", ...);
group.MapDelete("/competitors/{id}", ...);
group.MapPost("/seasons/{seasonId}/competitors/{competitorId}", ...);
group.MapDelete("/seasons/{seasonId}/competitors/{competitorId}", ...);

// Events (similar pattern)
group.MapGet("/seasons/{seasonId}/events", ...);
group.MapGet("/events/{id}", ...);
group.MapPost("/events", ...);
group.MapPut("/events/{id}", ...);
group.MapDelete("/events/{id}", ...);

// Event Results
group.MapGet("/events/{eventId}/result", ...);
group.MapPost("/events/{eventId}/result", ...);
group.MapDelete("/events/{eventId}/result", ...);

// Users
group.MapGet("/users", ...);
group.MapGet("/users/{id}", ...);
group.MapPut("/users/{id}", ...);
group.MapDelete("/users/{id}", ...);
```

### Service Registration

Ensure **Program.cs** (API project) registers AdminStateService:

```csharp
builder.Services.AddScoped<AdminStateService>();
```

And ensure all repositories are registered:
```csharp
builder.Services.AddScoped<IDisciplineRepository, DisciplineRepository>();
builder.Services.AddScoped<ISeriesRepository, SeriesRepository>();
builder.Services.AddScoped<ISeasonRepository, SeasonRepository>();
builder.Services.AddScoped<ICompetitorRepository, CompetitorRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
```

### Full Management Pages

To replace placeholder pages, create complete CRUD pages following the **DisciplineManagement.razor** pattern:

1. **SeriesManagement.razor** - route `/admin/series`
2. **SeasonManagement.razor** - route `/admin/seasons`
3. **CompetitorManagement.razor** - route `/admin/competitors`
4. **EventManagement.razor** - route `/admin/events`
5. **EventResultsManagement.razor** - route `/admin/results`
6. **UserManagement.razor** - route `/admin/users`

Each should include:
- List view with table
- Create/Edit form
- Delete confirmation
- Loading and error states
- Proper admin authorization check

## ?? Implementation Pattern

### Repository Method Pattern
```csharp
public async Task<Entity?> CreateEntityAsync(Entity entity)
{
    var tableClient = _tableClientFactory.GetTableClient(TableName);
    try
    {
        entity.Id = Guid.NewGuid().ToString();
        entity.CreatedDate = DateTime.UtcNow;
        
        var tableEntity = new TableEntity(PartitionKey, entity.Id)
        {
            // Map properties
        };
        
        await tableClient.AddEntityAsync(tableEntity);
        return entity;
    }
    catch (RequestFailedException)
    {
        return null;
    }
}
```

### API Endpoint Pattern
```csharp
group.MapPost("/entities", async (
    [FromBody] CreateEntityRequest request,
    [FromServices] IEntityRepository repo) =>
{
    var entity = new Entity
    {
        // Map from request
    };
    
    var result = await repo.CreateEntityAsync(entity);
    if (result == null)
        return Results.BadRequest(new { error = "Failed to create entity" });
    
    return Results.Ok(result);
})
.RequireAdmin()
.WithName("CreateEntity");
```

## ?? Security Notes

- All admin endpoints use `.RequireAdmin()` filter
- Admin management endpoints use `.RequireAdminManagement()` filter
- Frontend checks `AdminState.IsAdmin` before rendering admin UI
- Frontend checks `AdminState.CanManageAdmins` for admin management
- Cannot delete yourself from admin list

## ?? UI Patterns

All admin pages follow consistent design:
- Header with title and primary action button
- Forms appear above tables when creating/editing
- Tables with status badges and action buttons
- Confirmation dialogs for destructive actions
- Loading states and error messages
- Responsive grid layouts

## ? Current Working Features

You can currently:
1. ? Access admin dashboard at `/admin` (if admin)
2. ? View all disciplines
3. ? Create/edit/delete disciplines
4. ? View all admins
5. ? Create/edit/remove admin privileges
6. ? Check for duplicate active seasons
7. ? Navigate to placeholder pages for other entities

## ?? Priority Next Steps

1. Complete CRUD for repositories (highest priority for API functionality)
2. Add API endpoints to AdminEndpoints.cs
3. Replace placeholder management pages with full implementations
4. Test end-to-end admin workflows
5. Add validation and error handling improvements
