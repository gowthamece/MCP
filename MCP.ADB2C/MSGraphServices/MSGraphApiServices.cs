using Azure.Identity;
using MCP.ADB2C.Models;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace MCP.ADB2C.MSGraphServices
{
    public class MSGraphApiServices: IMSGraphAPIServices
    {
        private static readonly string GraphApiUrl = "https://graph.microsoft.com/v1.0/";
        private static readonly string Scope = "https://graph.microsoft.com/.default";
        private readonly AzureAdOptions _azureAdOptions;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _tenantId;
        private readonly IConfiguration _configuration;
        public MSGraphApiServices(IOptions<AzureAdOptions> azureAdOptions, IConfiguration configuration)
        {
            _configuration = configuration;
            _azureAdOptions = azureAdOptions.Value;
            _clientId = _azureAdOptions.ClientId;
            _clientSecret = _azureAdOptions.ClientSecret;
            _tenantId = _azureAdOptions.TenantId;
        }
        public GraphServiceClient GetGraphClientAsync()
        {
            var scopes = new[] { Scope };
            var options = new ClientSecretCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
            };

            var clientSecretCredential = new ClientSecretCredential(
                _configuration["ADB2C:TenantId"],
               "628a6be0-30a8-4d34-a377-b80a1797b711",
                _configuration["ADB2C:ClientSecret"],
                options);

            return new GraphServiceClient(clientSecretCredential, scopes);
        }

        public async Task<List<Users>> GetUsersAsync()
        {
            var graphClient = GetGraphClientAsync();
            var users = await graphClient.Users.GetAsync();
            var userList = new List<Users>(users.Value.Count);

            foreach (var userObj in users.Value)
            {
                var result = await graphClient.Users[userObj.Id].GetAsync(rc =>
                {
                    rc.QueryParameters.Select = new[] { "identities" };
                });
                var email = result.Identities?.FirstOrDefault(e => e.SignInType == "emailAddress")?.IssuerAssignedId ?? string.Empty;
                userList.Add(new Users
                {
                    Id = userObj.Id,
                    DispalyName = userObj.DisplayName,
                    Email = email,
                    Type = "User"
                });
            }
            return userList;
        }

        public async Task<List<Users>> GetMembersByTypeAndFilterAsync(string memberType, string searchFieldValue)
        {
            var membersList = new List<Users>();
            try
            {
                var graphClient = GetGraphClientAsync();
                var issuer = _configuration["ADB2C:Issuer"];
                if (memberType.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    var users = await graphClient.Users
                        .GetAsync(rc =>
                        {
                            rc.QueryParameters.Filter = $"identities/any(c:c/issuerAssignedId eq '{searchFieldValue}' and c/issuer eq '{issuer}')";
                            rc.QueryParameters.Select = new[] { "id", "displayName", "identities" };
                        });

                    foreach (var user in users.Value)
                    {
                        var emailFromIdentities = user.Identities?.FirstOrDefault(id => id.SignInType == "emailAddress")?.IssuerAssignedId;
                        membersList.Add(new Users
                        {
                            Id = user.Id,
                            DispalyName = $"{user.DisplayName} - {emailFromIdentities}",
                            Email = emailFromIdentities,
                            Type = "User"
                        });
                    }
                }
                //else if (memberType.Equals("group", StringComparison.OrdinalIgnoreCase))
                //{
                //    var groups = await graphClient.Groups
                //        .GetAsync(rc =>
                //        {
                //            rc.QueryParameters.Filter = $"startswith(displayName, '{searchFieldValue}')";
                //            rc.QueryParameters.Select = new[] { "id", "displayName", "mail" };
                //        });

                //    foreach (var group in groups.Value)
                //    {
                //        membersList.Add(new Users
                //        {
                //            Id = group.Id,
                //            DispalyName = group.DisplayName,
                //            Email = group.Mail,
                //            Type = "Group"
                //        });
                //    }
                //}
            }
            catch
            {
                // Log or handle as needed
            }
            return membersList;
        }

        public async Task<List<MCP.ADB2C.Models.Application>> GetApplicationsAsync()
        {
            var graphClient = GetGraphClientAsync();
            var applications = await graphClient.Applications.GetAsync();
            var applicationList = new List<MCP.ADB2C.Models.Application>(applications.Value.Count);

            foreach (var app in applications.Value)
            {
                applicationList.Add(new MCP.ADB2C.Models.Application
                {
                    Id = app.Id,
                    Name = app.DisplayName,
                    appId = app.AppId
                });
            }
            return applicationList;
        }

        public async Task<List<MCP.ADB2C.Models.Application>> GetApplicationsOwnedByUserAsync(string userId)
        {
            var ownedApplications = new List<MCP.ADB2C.Models.Application>();
            try
            {
                var graphClient = GetGraphClientAsync();
                var applications = await graphClient.Users[userId].OwnedObjects
                    .GetAsync(rc =>
                    {
                        rc.QueryParameters.Select = new[] { "id", "displayName", "appId" };
                    });

                if (applications?.Value != null)
                {
                    foreach (var directoryObject in applications.Value)
                    {
                        if (directoryObject is Microsoft.Graph.Models.Application app)
                        {
                            ownedApplications.Add(new MCP.ADB2C.Models.Application
                            {
                                Id = app.Id,
                                Name = app.DisplayName,
                                appId = app.AppId
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
               // _logger.LogError(ex, "Error getting applications owned by user");
            }
            return ownedApplications;
        }

        public async Task<List<MCP.ADB2C.Models.AppRole>> GetAppRolesAsync(string appName)
        {
            var appRole = new List<MCP.ADB2C.Models.AppRole>();
            var graphClient = GetGraphClientAsync();
            var application = await graphClient.Applications
                .GetAsync(rc =>
                {
                    rc.QueryParameters.Filter = $"displayName eq '{appName}'";
                    rc.QueryParameters.Select = new[] { "appRoles", "appId" };
                });
            var appRoles = application?.Value?.FirstOrDefault()?.AppRoles;
            var clientId = application?.Value?.FirstOrDefault()?.AppId;
            if (appRoles != null)
            {
                foreach (var role in appRoles)
                {
                    appRole.Add(new MCP.ADB2C.Models.AppRole
                    {
                        Id = role.Id.ToString(),
                        Name = role.DisplayName,
                        AppId = clientId
                    });
                }
            }
            return appRole;
        }

        public async Task<List<Users>> GetUserByAppRoleId(string roleName, string appName)
        {
            try
            {
                var graphClient = GetGraphClientAsync();
                var servicePrincipals = await graphClient.ServicePrincipals
                    .GetAsync(rc =>
                    {
                        rc.QueryParameters.Filter = $"displayName eq '{appName}'";
                    });
                var servicePrincipal = servicePrincipals?.Value?.FirstOrDefault();
                if (servicePrincipal == null) return new List<Users>();

                var assignedUsers = await graphClient.ServicePrincipals[servicePrincipal.Id]
                    .AppRoleAssignedTo.GetAsync();
                // Get the roleId from the roleName
                var appRoles = await GetAppRolesAsync(appName);
                var role = appRoles.FirstOrDefault(r => r.Name == roleName);
                if (role == null)
                    return new List<Users>(); // or handle as needed

                var roleId = role.Id;
                var assignedUsersResult = assignedUsers.Value
                    .Where(e => e. AppRoleId.ToString() == roleId);

                var userList = new List<Users>();
                foreach (var userObj in assignedUsersResult)
                {
                    if (userObj.PrincipalType.Equals("user", StringComparison.OrdinalIgnoreCase))
                    {
                        var userId = userObj.PrincipalId.ToString();
                        var result = await graphClient.Users[userId].GetAsync(rc =>
                        {
                            rc.QueryParameters.Select = new[] { "identities" };
                        });
                        var email = result.Identities?.FirstOrDefault(e => e.SignInType == "emailAddress")?.IssuerAssignedId ?? string.Empty;
                        userList.Add(new Users
                        {
                            Id = userObj.PrincipalId.ToString(),
                            DispalyName = userObj.PrincipalDisplayName,
                            Email = email,
                            Type = userObj.PrincipalType
                        });
                    }
                    else
                    {
                        userList.Add(new Users
                        {
                            Id = userObj.PrincipalId.ToString(),
                            DispalyName = userObj.PrincipalDisplayName,
                            Email = string.Empty,
                            Type = userObj.PrincipalType
                        });
                    }
                }
                return userList;
            }
            catch
            {
                return await GetUsersAsync();
            }
        }

        public async Task AssignUserToAppRole(string userId, string appName, string roleName, string memberType = "user")
        {
            try
            {
                var graphClient = GetGraphClientAsync();
                var servicePrincipals = await graphClient.ServicePrincipals
                    .GetAsync(rc =>
                    {
                        rc.QueryParameters.Filter = $"displayName eq '{appName}'";
                    });
                var servicePrincipal = servicePrincipals?.Value?.FirstOrDefault();
                if (servicePrincipal == null) return;

                var appRoles = await GetAppRolesAsync(appName);
                var role = appRoles.FirstOrDefault(r => r.Name == roleName);
               
                var roleId = role.Id;
                var requestBody = new AppRoleAssignment
                {
                    PrincipalId = Guid.Parse(userId),
                    ResourceId = Guid.Parse(servicePrincipal.Id),
                    AppRoleId = Guid.Parse(roleId)
                };

                if (memberType.Equals("user", StringComparison.OrdinalIgnoreCase))
                    await graphClient.Users[userId].AppRoleAssignments.PostAsync(requestBody);
                else
                    await graphClient.Groups[servicePrincipal.AppId].AppRoleAssignments.PostAsync(requestBody);
            }
            catch
            {
                // Log or handle as needed
            }
        }

        public async Task RevokeMemberFromAppRole(string principalId, string appName, string roleName, string memberType = "user")
        {
            try
            {
                var graphClient = GetGraphClientAsync();
                var servicePrincipals = await graphClient.ServicePrincipals
                    .GetAsync(rc =>
                    {
                        rc.QueryParameters.Filter = $"displayName eq '{appName}'";
                    });
                var servicePrincipal = servicePrincipals?.Value?.FirstOrDefault();
                if (servicePrincipal == null) return;

                var assignments = memberType.Equals("user", StringComparison.OrdinalIgnoreCase)
                    ? await graphClient.Users[principalId].AppRoleAssignments.GetAsync()
                    : await graphClient.Groups[principalId].AppRoleAssignments.GetAsync();

                var appRoles = await GetAppRolesAsync(appName);
                var role = appRoles.FirstOrDefault(r => r.Name == roleName);
                var appRoleId = role.Id;
                var assignmentToRevoke = assignments.Value.FirstOrDefault(a =>
                    a.ResourceId == Guid.Parse(servicePrincipal.Id) &&
                    a.AppRoleId == Guid.Parse(appRoleId));

                if (assignmentToRevoke != null)
                {
                    if (memberType.Equals("user", StringComparison.OrdinalIgnoreCase))
                        await graphClient.Users[principalId].AppRoleAssignments[assignmentToRevoke.Id].DeleteAsync();
                    else
                        await graphClient.Groups[principalId].AppRoleAssignments[assignmentToRevoke.Id].DeleteAsync();
                }
            }
            catch
            {
                // Log or handle as needed
            }
        }

        public async Task<List<UserRoleInfo>> GetUserRolesFromAllApplicationsAsync(string userId)
        {
            var userRoleInfoList = new List<UserRoleInfo>();
            try
            {
                var graphClient = GetGraphClientAsync();
                
                // Get user information
                var user = await graphClient.Users[userId].GetAsync(rc =>
                {
                    rc.QueryParameters.Select = new[] { "id", "displayName", "identities" };
                });
                
                if (user == null) return userRoleInfoList;
                
                var userEmail = user.Identities?.FirstOrDefault(e => e.SignInType == "emailAddress")?.IssuerAssignedId ?? string.Empty;
                
                // Get all user's app role assignments
                var appRoleAssignments = await graphClient.Users[userId].AppRoleAssignments.GetAsync();
                
                if (appRoleAssignments?.Value == null) return userRoleInfoList;
                
                // Get all applications to match with assignments
                var applications = await graphClient.Applications.GetAsync();
                var servicePrincipals = await graphClient.ServicePrincipals.GetAsync();
                
                foreach (var assignment in appRoleAssignments.Value)
                {
                    try
                    {
                        // Find the service principal for this assignment
                        var servicePrincipal = servicePrincipals?.Value?.FirstOrDefault(sp => sp.Id == assignment.ResourceId.ToString());
                        if (servicePrincipal == null) continue;
                        
                        // Find the corresponding application
                        var application = applications?.Value?.FirstOrDefault(app => app.AppId == servicePrincipal.AppId);
                        if (application == null) continue;
                        
                        // Find the specific role
                        var appRole = application.AppRoles?.FirstOrDefault(role => role.Id == assignment.AppRoleId);
                        if (appRole == null) continue;
                        
                        userRoleInfoList.Add(new UserRoleInfo
                        {
                            UserId = userId,
                            UserDisplayName = user.DisplayName ?? string.Empty,
                            UserEmail = userEmail,
                            ApplicationId = application.AppId ?? string.Empty,
                            ApplicationName = application.DisplayName ?? string.Empty,
                            RoleId = appRole.Id?.ToString() ?? string.Empty,
                            RoleName = appRole.DisplayName ?? string.Empty,
                            AssignedDate = assignment.CreatedDateTime?.DateTime ?? DateTime.MinValue
                        });
                    }
                    catch
                    {
                        // Skip this assignment if there's an error processing it
                        continue;
                    }
                }
            }
            catch
            {
                // Log or handle as needed
            }
            
            return userRoleInfoList;
        }

        public async Task<bool> CreateAppRoleAsync(string appName, string roleName)
        {
            try
            {
                var graphClient = GetGraphClientAsync();
                var app = await graphClient.Applications
             .GetAsync(rc =>
             {
                 rc.QueryParameters.Filter = $"displayName eq '{appName}'";
                 rc.QueryParameters.Select = new[] { "appRoles", "appId", "Id" };
             });
              
                var appId = app?.Value?.FirstOrDefault()?.Id;
                var application = await graphClient.Applications[appId]
                    .GetAsync(rc =>
                    {
                        rc.QueryParameters.Select = new[] { "appRoles" };
                    });

                if (application == null)
                    return false;

                var newAppRole = new Microsoft.Graph.Models.AppRole
                {
                    Id = Guid.NewGuid(),
                    DisplayName = roleName,
                    Description = $"{roleName} role created for {appId}",
                    IsEnabled = true,
                    Value = roleName,
                    AllowedMemberTypes = new List<string> { "User" }
                };

                var updatedAppRoles = application.AppRoles ?? new List<Microsoft.Graph.Models.AppRole>();
                updatedAppRoles.Add(newAppRole);

                var updatedApplication = new Microsoft.Graph.Models.Application
                {
                    AppRoles = updatedAppRoles
                };

                await graphClient.Applications[appId].PatchAsync(updatedApplication);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<MCP.ADB2C.Models.Application>> GetApplicationsOptimizedAsync(string? userId = null, bool ownedOnly = false)
        {
            var applicationList = new List<MCP.ADB2C.Models.Application>();
            try
            {
                var graphClient = GetGraphClientAsync();
                
                if (ownedOnly && !string.IsNullOrEmpty(userId))
                {
                    // Get applications owned by the user
                    var ownedApplications = await graphClient.Users[userId].OwnedObjects
                        .GetAsync(rc =>
                        {
                            rc.QueryParameters.Select = new[] { "id", "displayName", "appId" };
                        });

                    if (ownedApplications?.Value != null)
                    {
                        foreach (var directoryObject in ownedApplications.Value)
                        {
                            if (directoryObject is Microsoft.Graph.Models.Application app)
                            {
                                applicationList.Add(new MCP.ADB2C.Models.Application
                                {
                                    Id = app.Id ?? string.Empty,
                                    Name = app.DisplayName ?? string.Empty,
                                    appId = app.AppId ?? string.Empty
                                });
                            }
                        }
                    }
                }
                else
                {
                    // Get all applications
                    var applications = await graphClient.Applications.GetAsync();
                    
                    if (applications?.Value != null)
                    {
                        foreach (var app in applications.Value)
                        {
                            applicationList.Add(new MCP.ADB2C.Models.Application
                            {
                                Id = app.Id ?? string.Empty,
                                Name = app.DisplayName ?? string.Empty,
                                appId = app.AppId ?? string.Empty
                            });
                        }
                    }
                }
            }
            catch
            {
                // Log or handle as needed
            }
            
            return applicationList;
        }

        public async Task<object> ManageRolesOptimizedAsync(string action, string? appName = null, string? username = null, string? roleName = null)
        {
            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "get-roles":
                        if (string.IsNullOrEmpty(appName))
                            return "Application name is required for get-roles action.";
                        return await GetAppRolesAsync(appName);

                    case "assign-role":
                        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(roleName))
                            return "Username, application name, and role name are required for assign-role action.";
                        
                        var users = await GetMembersByTypeAndFilterAsync("user", username);
                        var user = users.FirstOrDefault(u => u.Email.Equals(username, StringComparison.OrdinalIgnoreCase));
                        if (user == null)
                            return $"User {username} not found.";
                        
                        await AssignUserToAppRole(user.Id, appName, roleName, "user");
                        return $"{username} assigned successfully to the role {roleName}";

                    case "revoke-role":
                        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(roleName))
                            return "Username, application name, and role name are required for revoke-role action.";
                        
                        var usersRevoke = await GetMembersByTypeAndFilterAsync("user", username);
                        var userRevoke = usersRevoke.FirstOrDefault(u => u.Email.Equals(username, StringComparison.OrdinalIgnoreCase));
                        if (userRevoke == null)
                            return $"User {username} not found.";
                        
                        await RevokeMemberFromAppRole(userRevoke.Id, appName, roleName, "user");
                        return $"{username} revoked successfully from the role {roleName}";

                    case "get-user-roles":
                        if (string.IsNullOrEmpty(username))
                            return "Username is required for get-user-roles action.";
                        
                        var usersRoles = await GetMembersByTypeAndFilterAsync("user", username);
                        var userRoles = usersRoles.FirstOrDefault(u => u.Email.Equals(username, StringComparison.OrdinalIgnoreCase));
                        if (userRoles == null)
                            return new List<MCP.ADB2C.Models.UserRoleInfo>();
                        
                        return await GetUserRolesFromAllApplicationsAsync(userRoles.Id);

                    case "create-role":
                        if (string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(roleName))
                            return "Application name and role name are required for create-role action.";
                        
                        var result = await CreateAppRoleAsync(appName, roleName);
                        return result ? "App role created successfully." : "Failed to create app role.";

                    default:
                        return "Invalid action. Valid actions are: get-roles, assign-role, revoke-role, get-user-roles, create-role";
                }
            }
            catch (Exception ex)
            {
                return $"Error performing role action '{action}': {ex.Message}";
            }
        }

        public async Task<object> GetUsersOptimizedAsync(string action, string? roleName = null, string? appName = null)
        {
            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "all":
                        // Get all users
                        return await GetUsersAsync();

                    case "by-role":
                        if (string.IsNullOrEmpty(roleName) || string.IsNullOrEmpty(appName))
                            return "Role name and application name are required for by-role action.";
                        
                        return await GetUserByAppRoleId(roleName, appName);

                    default:
                        return "Invalid action. Valid actions are: all, by-role";
                }
            }
            catch (Exception ex)
            {
                return $"Error performing user action '{action}': {ex.Message}";
            }
        }

    }
}
