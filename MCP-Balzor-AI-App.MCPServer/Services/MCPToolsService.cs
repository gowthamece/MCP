using Microsoft.Extensions.Logging;
using Mcp.Net.Server;
using Mcp.Net.Core;
using Mcp.Net.Core.Models.Tools;
using Mcp.Net.Core.Models.Content;
using System.Text.Json;

namespace MCP_Balzor_AI_App.MCPServer.Services
{
    public class MCPToolsService
    {
        private readonly ILogger<MCPToolsService> _logger;
        private readonly IGraphService _graphService;

        public MCPToolsService(ILogger<MCPToolsService> logger, IGraphService graphService)
        {
            _logger = logger;
            _graphService = graphService;
        }

        public async Task RegisterToolsAsync(McpServer server)
        {
            // Register the "get_user_profile" tool
            var getUserProfileSchema = JsonSerializer.Serialize(new
            {
                name = "get_user_profile",
                description = "Retrieve user profile details from Microsoft Graph API by email address",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        email = new
                        {
                            type = "string",
                            description = "The email address of the user to retrieve profile for"
                        }
                    },
                    required = new[] { "email" }
                }
            });
            
            server.RegisterTool("get_user_profile", "Retrieve user profile details from Microsoft Graph API by email address", 
                JsonDocument.Parse(getUserProfileSchema).RootElement, async (args) => await ExecuteGetUserProfileAsync(args));

            // Register the "get_current_user_profile" tool
            var getCurrentUserProfileSchema = JsonSerializer.Serialize(new
            {
                name = "get_current_user_profile",
                description = "Retrieve current authenticated user profile details from Microsoft Graph API",
                inputSchema = new
                {
                    type = "object",
                    properties = new { }
                }
            });
            
            server.RegisterTool("get_current_user_profile", "Retrieve current authenticated user profile details from Microsoft Graph API", 
                JsonDocument.Parse(getCurrentUserProfileSchema).RootElement, async (args) => await ExecuteGetCurrentUserProfileAsync(args));

            _logger.LogInformation("MCP tools registered successfully");
        }

        private async Task<ToolCallResult> ExecuteGetUserProfileAsync(JsonElement? arguments)
        {
            try
            {
                if (!arguments.HasValue)
                {
                    return new ToolCallResult
                    {
                        Content = new ContentBase[]
                        {
                            new TextContent { Text = "Error: Arguments are required" }
                        },
                        IsError = true
                    };
                }

                var email = arguments.Value.GetProperty("email").GetString();
                
                if (string.IsNullOrEmpty(email))
                {
                    return new ToolCallResult
                    {
                        Content = new ContentBase[]
                        {
                            new TextContent { Text = "Error: Email address is required" }
                        },
                        IsError = true
                    };
                }

                _logger.LogInformation("Getting user profile for email: {Email}", email);
                
                // Call the GraphService to get user profile data (now with real Microsoft Graph API)
                var profileData = await _graphService.GetUserProfileAsync(email);
                
                return new ToolCallResult
                {
                    Content = new ContentBase[]
                    {
                        new TextContent { Text = $"User Profile for {email}:\n\n{profileData}" }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing get_user_profile tool");
                return new ToolCallResult
                {
                    Content = new ContentBase[]
                    {
                        new TextContent { Text = $"Error retrieving user profile: {ex.Message}" }
                    },
                    IsError = true
                };
            }
        }

        private async Task<ToolCallResult> ExecuteGetCurrentUserProfileAsync(JsonElement? arguments)
        {
            try
            {
                _logger.LogInformation("Getting current user profile");
                
                // Call the GraphService to get current user profile data (now with real Microsoft Graph API)
                var profileData = await _graphService.GetCurrentUserProfileAsync();
                
                return new ToolCallResult
                {
                    Content = new ContentBase[]
                    {
                        new TextContent { Text = $"Current User Profile:\n\n{profileData}" }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing get_current_user_profile tool");
                return new ToolCallResult
                {
                    Content = new ContentBase[]
                    {
                        new TextContent { Text = $"Error retrieving current user profile: {ex.Message}" }
                    },
                    IsError = true
                };
            }
        }
    }
}
