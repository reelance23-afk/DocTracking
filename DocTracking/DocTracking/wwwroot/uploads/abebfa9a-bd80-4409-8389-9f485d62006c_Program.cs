using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DocTracking.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<DocumentService>();

await builder.Build().RunAsync();
