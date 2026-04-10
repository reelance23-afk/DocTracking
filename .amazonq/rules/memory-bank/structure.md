# DocTracking - Project Structure

## Solution Layout
```
DocTracking.sln
└── DocTracking/
    ├── DocTracking/          # Server project (ASP.NET Core host)
    └── DocTracking.Client/   # Client project (Blazor WebAssembly)
```

## DocTracking (Server Project)
ASP.NET Core 9 host that serves both the Blazor app and REST API.

```
DocTracking/
├── Controllers/              # REST API controllers
│   ├── AccountController.cs          # Login/Logout via OIDC
│   ├── DocumentsController.cs        # Core document CRUD + routing actions
│   ├── DocumentLogsController.cs     # Audit log endpoints + CSV export
│   ├── AppUsersController.cs         # User management
│   ├── OfficesController.cs          # Office management
│   ├── UnitsController.cs            # Unit management
│   └── NotificationsController.cs    # Broadcast notifications
├── Data/
│   ├── ApplicationDbContext.cs       # EF Core DbContext with all entity sets
│   └── ApplicationDbContextFactory.cs
├── Hubs/
│   └── NotificationHub.cs            # SignalR hub for real-time notifications
├── Services/
│   └── DocumentQueryService.cs       # All complex EF queries (separated from controllers)
├── Security/
│   └── UserClaimsTransformation.cs   # Enriches claims with DB role/office data
├── Migrations/                        # EF Core migration files
├── Pages/
│   └── Track.cshtml                  # Razor page for public QR tracking
├── Components/                        # Blazor server-side components (App.razor)
├── Shared/                            # Server-side shared Blazor components
├── Program.cs                         # App bootstrap, DI, middleware pipeline
├── CookieHandler.cs                   # DelegatingHandler to forward auth cookies
└── PersistingServerAuthenticationStateProvider.cs
```

## DocTracking.Client (Blazor WebAssembly Project)
Runs in the browser. Communicates with the server via HTTP and SignalR.

```
DocTracking.Client/
├── Pages/
│   ├── Admin/                # Admin-only pages
│   │   ├── AdminMaster.razor         # Admin dashboard
│   │   ├── AdminTracking.razor       # Admin document tracking view
│   │   ├── ManageOffice.razor        # Office/unit CRUD
│   │   ├── ManageUsers.razor         # User management
│   │   ├── AuditLog.razor            # Full audit log viewer
│   │   └── Tracking.razor            # Admin tracking overview
│   ├── Offices/              # Office staff pages
│   │   ├── OfficeDesk.razor          # Documents currently at this office
│   │   ├── OfficeHome.razor          # Office dashboard
│   │   ├── OfficeTracking.razor      # Office document tracking
│   │   └── UnitHistory.razor         # Unit document history
│   ├── Users/                # Regular user pages
│   │   ├── UserHome.razor            # User dashboard
│   │   ├── CreateDocument.razor      # Document submission form
│   │   ├── UserTracking.razor        # User's document tracking
│   │   └── MyTracking.razor          # Personal tracking view
│   └── Shared/               # Shared page components
│       ├── DeskDocCard.razor         # Document card for desk view
│       ├── DocStatCards.razor        # Stat summary cards
│       └── TrackingTimeline.razor    # Document history timeline
├── Layout/
│   ├── MainLayout.razor              # App shell with nav and theme
│   ├── NavMenu.razor                 # Sidebar navigation
│   └── NotificationBell.razor        # Real-time notification bell
├── Models/                   # Shared data models (used by both projects)
│   ├── Document.cs
│   ├── AppUser.cs
│   ├── Office.cs
│   ├── Unit.cs
│   ├── DocumentLog.cs
│   ├── AppNotification.cs
│   └── PagedResult.cs
├── Services/
│   ├── DocumentService.cs            # HTTP client wrapper for all API calls
│   ├── NotificationService.cs        # SignalR client + notification state
│   ├── ThemeService.cs               # Dark/light theme persistence
│   └── Helpers/
│       ├── DocHelpers.cs             # Document display utilities (colors, icons, labels)
│       └── DebounceHelper.cs         # Debounce timer for search inputs
└── Program.cs                        # WASM bootstrap, DI registration
```

## Core Architectural Patterns

### Blazor United (Auto Render Mode)
- Server project hosts both Interactive Server and WebAssembly render modes
- Client project is the WASM assembly loaded by the server
- Routes.razor handles routing; App.razor is the root component

### API Layer
- All data access goes through REST controllers under `/api/[controller]`
- Controllers are thin — complex queries delegated to `DocumentQueryService`
- `[Authorize]` + `[EnableRateLimiting("api")]` applied at controller level
- Role-based authorization uses `[Authorize(Roles = "Admin,Office")]`

### Real-Time Notifications
- SignalR hub at `/hubs/notifications`
- Clients join groups: `user-{id}`, `unit-{unitId}`, `office-{officeId}`, `office-head-{officeId}`
- Server pushes via `IHubContext<NotificationHub>` from controllers
- Notifications also persisted to `AppNotifications` table

### Authentication
- Azure AD / Microsoft Identity via OIDC (`Microsoft.Identity.Web`)
- Cookie-based session after OIDC login
- `UserClaimsTransformation` enriches claims with DB role, office, unit data
- `CookieHandler` forwards auth cookies from WASM to API calls

### Database
- EF Core with SQL Server (dev) / PostgreSQL (production via env vars)
- Auto-migration on startup
- Composite indexes on frequently filtered columns (Status, OfficeId, CreatorId)
- `DeleteBehavior.Restrict` on most foreign keys to prevent cascade deletes
