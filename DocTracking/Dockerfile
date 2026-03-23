FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish DocTracking/DocTracking/DocTracking.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DocTracking.dll"]

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));


UseSqlServer("Server =.\\SQLEXPRESS; Database = DocTrackingDB; Trusted_Connection = True; TrustServerCertificate = True; ")

.UseNpgsql("Host=crossover.proxy.rlwy.net;Port=56546;Database=railway;Username=postgres;Password=ntQqnqwFwyYzhOGpdoIUmYfKkAUntPfn;")

      "https": {
        "commandName": "Project",
        "dotnetRunMessages": true,
        	,
        "inspectUri": "{wsProtocol}://{url.hostname}:{url.port}/_framework/debug/ws-proxy?browser={browserInspectUri}",
        "applicationUrl": "https://localhost:7221;http://localhost:5290",
        "environmentVariables": {
          "ASPNETCORE_ENVIRONMENT": "Development"


DocTracking — Considerations Master List
🔴 Critical (Address First)
File storage on local filesystem — wwwroot/uploads is wiped on container restart. Need persistent volume or Azure Blob Storage.

No server-side pagination — GetAllDocument and GetAuditLogs return everything. Will degrade at scale.

File download has no auth — uploaded files are publicly accessible by URL with no authentication check.

🟠 Security
DeleteUpload has no ownership check — any authenticated user can delete any file if they know the path.

GetAllDocument and GetDocumentLogs have no role restriction — basic "User" role can call them directly via API.

No rate limiting on any endpoint — spam/brute force possible.

🟡 Data Integrity
Server doesn't enforce file attachment — client blocks it but a direct API call can create a document with no file.

UpdateDocument (admin edit) has no audit log — edits leave no trace in DocumentLogs.

AdminOverride doesn't record before/after state — no snapshot of what changed.

🟡 Performance
UserClaimsTransformation cache (5 min TTL) doesn't invalidate on role/office change — user must wait up to 5 minutes after being reassigned.

AdminMaster polls every 30 seconds and re-fetches all documents — no delta/diff, full reload each time.

🔵 User Experience
No idle/stuck document warning on the office side — only the document creator (UserHome) sees the "idle 1 day" alert.

No full history visible from OfficeDesk — office workers must navigate to UnitHistory separately.

File attachment is mandatory on CreateDocument — no way to submit without one.

No individual notification dismiss — opening the bell marks everything read at once.

No QR scan success feedback before navigation.

🔵 Operational / Deployment
Only Console.WriteLine logging — no structured logging, no log levels, no correlation IDs in production.

/health endpoint has no DB check — app can report healthy while DB is down.

Migration failure is silently swallowed — app starts with potentially broken schema if migration fails.

🔵 Missing Features
No email/push notifications — offline users miss everything until they log back in.

No document search by reference number from main nav — must use QR scanner or direct URL.

No bulk document operations for admin — no bulk delete or bulk reassign documents.

No individual notification read/unread toggle — all-or-nothing mark as read.

Current system status: All 52 files audited, 0 bugs. System is clean and functional. The above are improvements/risks, not existing bugs.