using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Mcp.Net.Server;
using Mcp.Net.Core.Models.Capabilities;
using MCP_Balzor_AI_App.MCPServer.Services;
using MCP_Balzor_AI_App.MCPServer;

var builder = WebApplication.CreateBuilder(args);

// Ensure we're using the correct base path for configuration
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add Web API services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add MCP services
builder.Services.AddSingleton<IGraphService, GraphService>();
builder.Services.AddSingleton<MCPToolsService>();

// Add the MCP Server as a hosted service
builder.Services.AddHostedService<MCPServerService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseRouting();
app.MapControllers();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting MCP Server Host with HTTP API...");

// Start the host (which will start both the MCP Server service and HTTP API)
await app.RunAsync();
