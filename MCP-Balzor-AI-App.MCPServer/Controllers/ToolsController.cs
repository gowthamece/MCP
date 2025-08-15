using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MCP_Balzor_AI_App.MCPServer.Services;
using System.Text.Json;

namespace MCP_Balzor_AI_App.MCPServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ToolsController : ControllerBase
    {
        private readonly IGraphService _graphService;
        private readonly ILogger<ToolsController> _logger;

        public ToolsController(IGraphService graphService, ILogger<ToolsController> logger)
        {
            _graphService = graphService;
            _logger = logger;
        }

        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteTool([FromBody] ToolExecutionRequest request)
        {
            try
            {
                _logger.LogInformation("HTTP API: Executing tool: {Tool} for email: {Email}", request.Tool, request.Email);

                switch (request.Tool?.ToLower())
                {
                    case "get_user_profile":
                        if (string.IsNullOrEmpty(request.Email))
                        {
                            return BadRequest("Email is required for get_user_profile tool");
                        }
                        var userProfile = await _graphService.GetUserProfileAsync(request.Email);
                        return Ok(userProfile);

                    case "get_current_user_profile":
                        var currentUserProfile = await _graphService.GetCurrentUserProfileAsync();
                        return Ok(currentUserProfile);

                    default:
                        return BadRequest($"Unknown tool: {request.Tool}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool: {Tool}", request.Tool);
                return StatusCode(500, $"Error executing tool: {ex.Message}");
            }
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
    }

    public class ToolExecutionRequest
    {
        public string? Tool { get; set; }
        public string? Email { get; set; }
    }
}
