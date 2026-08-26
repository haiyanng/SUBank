using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SUBank.Client;
using SUBank.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = string.IsNullOrWhiteSpace(builder.Configuration["ApiBaseUrl"])
    ? builder.HostEnvironment.BaseAddress
    : builder.Configuration["ApiBaseUrl"]!;
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<ApiSession>();
builder.Services.AddScoped<RealtimeService>();

await builder.Build().RunAsync();
