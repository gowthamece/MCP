using MCP.ADB2C.Models;
using MCP.ADB2C.MSGraphServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Threading.Tasks;

namespace MCP.ADB2C.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [McpServerToolType]
    public class UserController : ControllerBase
    {
        private readonly IMSGraphAPIServices _msGraphApiServices;
        
        public UserController(IMSGraphAPIServices msGraphApiServices) 
        {
            _msGraphApiServices = msGraphApiServices;
        }

        /// <summary>
        /// Unified endpoint for managing Azure AD B2C users with action-based routing
        /// </summary>
        /// <param name="action">Action to perform: 'all' (get all users) or 'by-role' (get users by role)</param>
        /// <param name="roleName">Role name (required for 'by-role' action)</param>
        /// <param name="appName">Application name (required for 'by-role' action)</param>
        [HttpGet("manage")]
        [McpServerTool(Name = "GetB2CUsers"), Description("Unified tool for managing Azure AD B2C users. Supports actions: 'all' (get all users), 'by-role' (get users by specific role and application).")]
        [Authorize]
        public async Task<IActionResult> GetUsersOptimized(
            [FromQuery] string action,
            [FromQuery] string? roleName = null,
            [FromQuery] string? appName = null)
        {
            try
            {
                var result = await _msGraphApiServices.GetUsersOptimizedAsync(action, roleName, appName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        //#region Legacy Endpoints (Deprecated - Use /manage instead)
        
        ///// <summary>
        ///// DEPRECATED: Gets all Azure AD B2C users. Use /manage?action=all instead
        ///// </summary>
        //[McpServerTool(Name = "GetADB2CUser"), Description("DEPRECATED: Get all B2C Users. Use GetB2CUsers with action='all' instead.")]
        //[Authorize]
        //[Obsolete("Use GetB2CUsers with action='all' instead")]
        //public async Task<IEnumerable<Users>> Get()
        //{
        //    return await _msGraphApiServices.GetUsersAsync();
        //}

        ///// <summary>
        ///// DEPRECATED: Gets users by application role. Use /manage?action=by-role&roleName={roleName}&appName={appName} instead
        ///// </summary>
        //[HttpGet("by-app-role", Name = "GetADB2CUsersByAppRole")]
        //[McpServerTool(Name = "GetADB2CUsersByAppRole"), Description("DEPRECATED: Get all users from the role associated to the Applications. Use GetB2CUsers with action='by-role' instead.")]
        //[Authorize]
        //[Obsolete("Use GetB2CUsers with action='by-role' instead")]
        //public async Task<List<MCP.ADB2C.Models.Users>> GetByAppRole(string appRole, string appName)
        //{
        //    var userList = await _msGraphApiServices.GetUserByAppRoleId(appRole, appName);
        //    return userList;
        //}
        
        //#endregion
    }
}
