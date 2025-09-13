using MCP.ADB2C.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;
using Application = MCP.ADB2C.Models.Application;

namespace MCP.ADB2C.MSGraphServices
{
    public interface IMSGraphAPIServices
    {
   //     Task<IActionResult> GetApplicationOwnerAuthorizationAsync(string email, string appId);
        Task<List<Users>> GetUsersAsync();
        //Task<List<Groups>> GetGroupsAsync();
        Task<List<Application>> GetApplicationsAsync();
        //Task<List<AppRole>> GetAppRolesAsync(string appId);
        //Task AssignUserToAppRole(string principalId, string resourceId, string appRoleId, string memberType = "user");
        //Task RevokeMemberFromAppRole(string principalId, string resourceId, string appRoleId, string memberType = "user");
        //Task<List<Users>> GetUserByAppRoleId(string roleId, string appId);
        //GraphServiceClient GetGraphClientAsync();
        //string GetCachedAccessToken(string tenantId, int secondsRemaining = 60);
        //void CacheAccessToken(string tenantId, string accesToken);
        Task<List<Users>> GetMembersByTypeAndFilterAsync(string memberType, string firstNameStartsWith);
        Task<List<Application>> GetApplicationsOwnedByUserAsync(string userId);
        Task<List<AppRole>> GetAppRolesAsync(string appId);
        Task<List<Users>> GetUserByAppRoleId(string roleName, string appName);
        Task AssignUserToAppRole(string userName, string appName, string appRoleId, string memberType = "user");
        Task RevokeMemberFromAppRole(string principalId, string resourceId, string appRoleId, string memberType = "user");
        Task<bool> CreateAppRoleAsync(string appName, string roleName);
        //Task<bool> CreateAppRoleAsync(string appId, AppRoleCreationRequest appRoleRequest);
        //Task<List<Groups>> GetGroupsOwnedByUserAsync(string userId);
        //Task<bool> AddUsersToGroupAsync(List<string> userIds, string groupId);
        //  Task<List<Users>> GetUsersByGroupIdAsync(string groupId);
        //  Task<bool> RemoveUserFromGroupAsync(string userId, string groupId);
        Task<List<UserRoleInfo>> GetUserRolesFromAllApplicationsAsync(string userId);
        Task<List<Application>> GetApplicationsOptimizedAsync(string? userId = null, bool ownedOnly = false);

        Task<object> ManageRolesOptimizedAsync(string action, string? appName = null, string? username = null, string? roleName = null);
        Task<object> GetUsersOptimizedAsync(string action, string? roleName = null, string? appName = null);
    }
}
