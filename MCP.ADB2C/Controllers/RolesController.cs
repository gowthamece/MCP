using MCP.ADB2C.MSGraphServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net;

namespace MCP.ADB2C.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [McpServerToolType]
    public class RolesController : ControllerBase
    {
        private readonly IMSGraphAPIServices _msGraphApiServices;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public RolesController(IMSGraphAPIServices msGraphApiServices, IHttpContextAccessor httpContextAccessor)
        {
            _msGraphApiServices = msGraphApiServices;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpGet("manage", Name = "ManageRoles")]
        [McpServerTool(Name = "ManageRoles"), Description("Unified tool for managing Azure AD B2C application roles. Actions: get-roles, assign-role, revoke-role, get-user-roles, create-role")]
        [Authorize]
        public async Task<object> ManageRoles(string action, string? appName = null, string? username = null, string? roleName = null)
        {
            return await _msGraphApiServices.ManageRolesOptimizedAsync(action, appName, username, roleName);
        }
    }
}

