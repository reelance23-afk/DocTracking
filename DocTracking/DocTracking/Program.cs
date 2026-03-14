using DocTracking;
using DocTracking.Client.Pages;
using DocTracking.Client.Services;
using DocTracking.Components;
using DocTracking.Data;
using DocTracking.Services;
using DocTracking.Security;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using MudBlazor.Services;
using Swashbuckle.AspNetCore.SwaggerUI;
using Microsoft.AspNetCore.Authentication;
using Npgsql.EntityFrameworkCore.PostgreSQL;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    var host = Environment.GetEnvironmentVariable("PGHOST");
    var port = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";
    var db = Environment.GetEnvironmentVariable("PGDATABASE");
    var user = Environment.GetEnvironmentVariable("PGUSER");
    var password = Environment.GetEnvironmentVariable("PGPASSWORD");
    connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true";
}

builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters.RoleClaimType = "roles";
    options.TokenValidationParameters.NameClaimType = "name";
});
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddHttpClient();
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.Bind("AzureAd", options);
        options.ResponseType = "code";
        options.SaveTokens = true;
    });
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, DocTracking.Services.PersistingServerAuthenticationStateProvider>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CookieHandler>();
builder.Services.AddHttpClient<DocumentService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["BaseAddress"] ?? "https://localhost:7221/");
}).AddHttpMessageHandler<CookieHandler>();

builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddMudServices();         
builder.Services.AddMudPopoverService();
builder.Services.AddSignalR();
builder.Services.AddTransient<IClaimsTransformation, UserClaimsTransformation>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                       Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        context.Request.Scheme = "https";
        await next();
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.ContentType?.Contains("text/html") == true)
    {
        context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "0";
    }
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(DocTracking.Client._Imports).Assembly);
app.MapGet("/health", () => Results.Ok());
app.MapControllers();
app.MapHub<DocTracking.Hubs.NotificationHub>("/hubs/notifications");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.Run();
