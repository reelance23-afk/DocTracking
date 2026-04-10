# DocTracking - Technology Stack

## Runtime & Framework
- .NET 9.0
- ASP.NET Core 9 (server host)
- Blazor WebAssembly 9 (client, `Microsoft.NET.Sdk.BlazorWebAssembly`)
- Blazor United / Auto render mode (server + WASM in one solution)

## UI
- MudBlazor 9.1.0 — component library for all UI (dialogs, tables, cards, icons)
- Bootstrap (bundled in wwwroot/lib) — minimal usage alongside MudBlazor
- Custom CSS: `mobile.css` (client), `app.css` (server wwwroot)

## Authentication & Authorization
- Microsoft Identity Web 4.3.0 (`Microsoft.Identity.Web`)
- Azure Active Directory via OpenID Connect
- Cookie-based session after OIDC login
- Role-based authorization: `Admin`, `Office`, `User`
- `UserClaimsTransformation` — enriches claims from DB on each request (cached 5 min via IMemoryCache)
- `CookieHandler` — DelegatingHandler that forwards auth cookies from WASM HttpClient to API

## Database
- Entity Framework Core 9.0.13
- Development: SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`)
- Production: PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4`)
- Database selected at runtime via environment variables (`PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, `PGPASSWORD`)
- Auto-migration on startup in `Program.cs`
- Data Protection keys persisted to DB (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore 9.0.3`)

## Real-Time
- ASP.NET Core SignalR (server hub: `NotificationHub`)
- Microsoft Azure SignalR Service 1.33.0 (production scale-out)
- SignalR Client 9.0.13 (WASM client)
- Hub endpoint: `/hubs/notifications`
- Group naming convention: `user-{id}`, `unit-{unitId}`, `office-{officeId}`, `office-head-{officeId}`

## File Storage
- Local filesystem uploads in `wwwroot/uploads/` (dev)
- Azure Blob Storage 12.27.0 (`Azure.Storage.Blobs`) — available for production
- Files named with GUID prefix: `{guid}_{originalName}`
- Max upload size: 10 MB

## QR Code
- QRCoder 1.7.0 — server-side QR code generation for documents

## API
- REST controllers under `/api/[controller]`
- Swagger/OpenAPI via Swashbuckle.AspNetCore 9.0.6 (dev only)
- Rate limiting: fixed window, 300 req/min per user/IP, policy name `"api"`
- JSON: `ReferenceHandler.IgnoreCycles` to handle circular navigation properties

## Key NuGet Packages (Server)
| Package | Version | Purpose |
|---|---|---|
| Microsoft.Identity.Web | 4.3.0 | Azure AD auth |
| Microsoft.Identity.Web.MicrosoftGraph | 3.8.1 | Graph API access |
| MudBlazor | 9.1.0 | UI components |
| Microsoft.EntityFrameworkCore | 9.0.13 | ORM |
| Npgsql.EntityFrameworkCore.PostgreSQL | 9.0.4 | PostgreSQL provider |
| Microsoft.Azure.SignalR | 1.33.0 | SignalR scale-out |
| Azure.Storage.Blobs | 12.27.0 | File storage |
| QRCoder | 1.7.0 | QR generation |
| Swashbuckle.AspNetCore | 9.0.6 | Swagger UI |

## Key NuGet Packages (Client)
| Package | Version | Purpose |
|---|---|---|
| Microsoft.AspNetCore.Components.WebAssembly | 9.0.12 | WASM runtime |
| Microsoft.Authentication.WebAssembly.Msal | 9.0.13 | MSAL auth in WASM |
| Microsoft.AspNetCore.SignalR.Client | 9.0.13 | SignalR client |
| MudBlazor | 9.1.0 | UI components |

## Configuration
- `appsettings.json` — AzureAd section (TenantId, ClientId, Domain), BaseAddress, ConnectionStrings
- `appsettings.Development.json` — dev overrides
- `wwwroot/appsettings.json` (client) — WASM-side config
- Production uses Railway.app hosting with PostgreSQL env vars

## Development Commands
```bash
# Run the server (serves both server + WASM)
dotnet run --project DocTracking/DocTracking/DocTracking.csproj

# Add EF migration
dotnet ef migrations add <MigrationName> --project DocTracking/DocTracking

# Apply migrations manually
dotnet ef database update --project DocTracking/DocTracking

# Build solution
dotnet build DocTracking.sln

# Publish
dotnet publish DocTracking/DocTracking/DocTracking.csproj -c Release
```

## Deployment
- Dockerfile present at `DocTracking/Dockerfile`
- GitHub Actions workflows in `.github/workflows/`
- Production hosted on Railway.app
- Production base address: `https://doctracking-production.up.railway.app/`
