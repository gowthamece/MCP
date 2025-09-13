
using MCP.ADB2C.MSGraphServices;
using MCP.ADB2C.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;
using System.Security.Claims;
using static System.Net.WebRequestMethods;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
//builder.Services.AddSwaggerGen();


var serverUrl = builder.Configuration["MCPServer:BaseUrl"] ?? "http://localhost:5156";
var tenantId = builder.Configuration["ADB2C:TenantId"] ?? throw new InvalidOperationException("TenantId not found in configuration");
var appClientId = builder.Configuration["ADB2C:ClientId"] ?? throw new InvalidOperationException("ClientId not found in configuration");
var issuer = builder.Configuration["ADB2C:Issuer"] ?? "gowthamcbe.onmicrosoft.com";
var scopeName = $"https://{issuer}/{appClientId}/Admin.Read";
var authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();
if (builder.Environment.IsProduction() || builder.Environment.IsStaging())
{

    Uri kvUri = new(builder.Configuration["AzureEndPoints:keyVaultName"] ?? "https://kv-ent-dev-eu-1.vault.azure.net");
    var options = new DefaultAzureCredentialOptions
    {
        ExcludeEnvironmentCredential = false
    };
    builder.Configuration.AddAzureKeyVault(kvUri, new DefaultAzureCredential());

}
builder.Services.AddAuthentication(options =>
{
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = authority;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAudience = appClientId,
        ValidIssuers = new[]
                 {
                     authority,
                     $"https://sts.windows.net/{tenantId}/",
                 },
        NameClaimType = "name",
        RoleClaimType = "roles",
    };
    options.MetadataAddress = $"{authority}/.well-known/openid-configuration";

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("🔍 JWT TOKEN RECEIVED");
            logger.LogInformation("Request Path: {Path}", context.Request.Path);
            
            if (context.Request.Headers.ContainsKey("Authorization"))
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                logger.LogInformation("Authorization Header Present: {HasHeader}", !string.IsNullOrEmpty(authHeader));
                if (!string.IsNullOrEmpty(authHeader))
                {
                    logger.LogInformation("Auth Header Preview: {Preview}...", 
                        authHeader.Length > 50 ? authHeader.Substring(0, 50) : authHeader);
                }
            }
            else
            {
                logger.LogWarning("❌ NO AUTHORIZATION HEADER");
            }
            
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var name = context.Principal?.Identity?.Name ?? "unknown";
            var upn = context.Principal?.FindFirstValue(
                   "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn") ?? "unknown";
            
            logger.LogInformation("✅ JWT TOKEN VALIDATED SUCCESSFULLY");
            logger.LogInformation("User Name: {Name}", name);
            logger.LogInformation("UPN: {Upn}", upn);
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Token validated for: {name} ({upn})");
            Console.ResetColor();
            
            // Log all claims for debugging
            logger.LogInformation("User Claims:");
            foreach (var claim in context.Principal.Claims.Take(10))
            {
                logger.LogInformation("  {Type}: {Value}", claim.Type, claim.Value);
            }
            
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("❌ JWT AUTHENTICATION FAILED");
            logger.LogError("Error: {Error}", context.Exception.Message);
            logger.LogError("Exception: {Exception}", context.Exception.ToString());
            
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Authentication failed: {context.Exception.Message}");
            Console.ResetColor();
            
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("🔐 JWT AUTHENTICATION CHALLENGE");
            logger.LogWarning("Error: {Error}", context.Error);
            logger.LogWarning("Error Description: {ErrorDescription}", context.ErrorDescription);
            
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"🔐 Authentication challenge: {context.Error} - {context.ErrorDescription}");
            Console.ResetColor();
            
            return Task.CompletedTask;
        }
    };
})
.AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        Resource = new Uri(serverUrl),
        AuthorizationServers = { new Uri(authority) },
        ScopesSupported = [$"{scopeName}"],
    };
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IMSGraphAPIServices, MSGraphApiServices>();

// Add logging configuration for detailed debugging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

// Add custom authorization logging middleware
app.UseMiddleware<AuthorizationLoggingMiddleware>();

//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}
app.UseExceptionHandler();
app.UseRouting();
//app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => "MCP server is running!");
app.MapControllers();
app.MapMcp().RequireAuthorization();
app.Run();
