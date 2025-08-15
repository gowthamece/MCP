using MCP_Balzor_AI_App.Components;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add controllers
builder.Services.AddControllers();


// Add authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

// Add authorization services for Blazor
builder.Services.AddAuthorizationCore();

builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    var postLogoutRedirectUri = builder.Configuration["AzureAd:PostLogoutRedirectUri"];

    options.Events = new OpenIdConnectEvents
    {
        OnRedirectToIdentityProviderForSignOut = context =>
        {
            if (!string.IsNullOrEmpty(postLogoutRedirectUri))
            {

                //context.ProtocolMessage.PostLogoutRedirectUri = $"https://{context.Request.Host}{postLogoutRedirectUri}";
                var logoutUri = $"https://login.microsoftonline.com/common/oauth2/v2.0/logout" +
                   $"?post_logout_redirect_uri={Uri.EscapeDataString($"https://{context.Request.Host}/{context.Request.Path}/{postLogoutRedirectUri}")}";

                context.Response.Redirect(logoutUri);
                context.HandleResponse();
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }
    };
});
// Register HttpClient
builder.Services.AddHttpClient();

// Register MCPClientService
builder.Services.AddSingleton<MCP_Balzor_AI_App.Services.RealMCPClientService>();
builder.Services.AddSingleton<MCP_Balzor_AI_App.Services.MCPClientService>();

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
