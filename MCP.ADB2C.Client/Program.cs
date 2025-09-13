using Azure.Identity;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MCP.ADB2C.Client.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add controllers
builder.Services.AddControllers();

if (builder.Environment.IsProduction() || builder.Environment.IsStaging())
{

    Uri kvUri = new(builder.Configuration["AzureEndPoints:keyVaultName"] ?? "https://kv-ent-dev-eu-1.vault.azure.net");
    var options = new DefaultAzureCredentialOptions
    {
        ExcludeEnvironmentCredential = false
    };
    builder.Configuration.AddAzureKeyVault(kvUri, new DefaultAzureCredential());

}
// Add authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.Bind("AzureAd", options);
        options.ClientSecret = builder.Configuration["AzureAd:ClientSecret"];
        options.SaveTokens = true;
        options.Scope.Add("https://gowthamcbe.onmicrosoft.com/df32c431-f94d-4802-aab8-f2615d1d2f01/User.Read");
        options.Prompt = "consent";
    }).EnableTokenAcquisitionToCallDownstreamApi(new string[] { builder.Configuration["DownstreamApi:Scopes"] }).AddInMemoryTokenCaches();

builder.Services.AddServerSideBlazor().AddMicrosoftIdentityConsentHandler();

// Add authorization services for Blazor
builder.Services.AddAuthorizationCore();


builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();
// Register HttpClient
builder.Services.AddHttpClient(); 

// Register HttpContextAccessor for accessing HTTP context in services
builder.Services.AddHttpContextAccessor();

// Register MCPClientService as Scoped instead of Singleton to match AuthenticationStateProvider
builder.Services.AddScoped<MCP.ADB2C.Client.Services.RealMCPClientService>();
builder.Services.AddScoped<MCP.ADB2C.Client.Services.MCPClientService>();
builder.Services.AddScoped<MCP.ADB2C.Client.Services.HttpMCPClientService>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
