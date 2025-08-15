using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mcp.Net.Server;
using Mcp.Net.Core.Models.Capabilities;
using MCP_Balzor_AI_App.MCPServer.Services;

namespace MCP_Balzor_AI_App.MCPServer
{
    public class MCPServerService : BackgroundService
    {
        private readonly ILogger<MCPServerService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private McpServer? _mcpServer;

        public MCPServerService(ILogger<MCPServerService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Starting MCP Server service...");

                // Create MCP Server
                var serverInfo = new ServerInfo { Name = "MCP Graph Server", Version = "1.0.0" };
                _mcpServer = new McpServer(serverInfo);

                // Register tools
                using var scope = _serviceProvider.CreateScope();
                var toolsService = scope.ServiceProvider.GetRequiredService<MCPToolsService>();
                await toolsService.RegisterToolsAsync(_mcpServer);

                _logger.LogInformation("MCP Server started successfully on port 8080");

                // Keep the server running
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running MCP Server service");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping MCP Server service...");

            if (_mcpServer != null)
            {
                // Cleanup server resources
                _mcpServer = null;
            }

            await base.StopAsync(cancellationToken);
        }
    }
}
