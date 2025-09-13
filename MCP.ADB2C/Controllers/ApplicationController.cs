using MCP.ADB2C.MSGraphServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MCP.ADB2C.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [McpServerToolType]
    public class ApplicationController : ControllerBase
    {
        private readonly IMSGraphAPIServices _msGraphApiServices;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public ApplicationController(IMSGraphAPIServices msGraphApiServices, IHttpContextAccessor httpContextAccessor)
        {
            _msGraphApiServices = msGraphApiServices;
            _httpContextAccessor = httpContextAccessor;
        }
        [HttpGet(Name = "GetB2CApplications")]
        [McpServerTool(Name = "GetB2CApplications"), Description("Get B2C Applications. Optionally filter to only applications owned by the user by setting ownedOnly parameter to true.")]
        [Authorize]
        public async Task<List<MCP.ADB2C.Models.Application>> Get([FromQuery] bool ownedOnly = false)
        {
            var userId = ownedOnly ? GetUserId() : null;
            var applicationList = await _msGraphApiServices.GetApplicationsOptimizedAsync(userId, ownedOnly);
            return applicationList;
        }
        private string? GetUserId()
        {

            if (User != null && User.Identity?.IsAuthenticated == true)
            {
                return User.FindFirst("oid")?.Value
                    ?? User.FindFirst("sub")?.Value
                    ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
            }

            // Fallback to HttpContextAccessor
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var user = httpContext.User;
                return user.FindFirst("oid")?.Value
                    ?? user.FindFirst("sub")?.Value
                    ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
            }

            return null;
        }
    }
}
