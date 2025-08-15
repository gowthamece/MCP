using Microsoft.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Azure.Identity;

namespace MCP_Balzor_AI_App.MCPServer.Services
{
    public class GraphService : IGraphService
    {
        private readonly ILogger<GraphService> _logger;
        private readonly IConfiguration _configuration;

        public GraphService(ILogger<GraphService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            // Add diagnostic logging
            _logger.LogInformation("GraphService constructor called");
            
            var clientId = _configuration["GraphApi:ClientId"];
            var clientSecret = _configuration["GraphApi:ClientSecret"];
            var tenantId = _configuration["GraphApi:TenantId"];
            
            _logger.LogInformation("GraphAPI Configuration: ClientId={ClientIdExists}, ClientSecret={ClientSecretExists}, TenantId={TenantIdExists}", 
                !string.IsNullOrEmpty(clientId) ? "EXISTS" : "MISSING",
                !string.IsNullOrEmpty(clientSecret) ? "EXISTS" : "MISSING", 
                !string.IsNullOrEmpty(tenantId) ? "EXISTS" : "MISSING");
                
            if (!string.IsNullOrEmpty(clientId))
            {
                _logger.LogInformation("ClientId starts with: {ClientIdPrefix}", clientId.Substring(0, Math.Min(8, clientId.Length)));
            }
        }

        private GraphServiceClient? CreateGraphServiceClient()
        {
            try
            {
                _logger.LogInformation("CreateGraphServiceClient called");
                
                var clientId = _configuration["GraphApi:ClientId"];
                var clientSecret = _configuration["GraphApi:ClientSecret"];
                var tenantId = _configuration["GraphApi:TenantId"];

                _logger.LogInformation("Reading configuration: ClientId={ClientIdLength}, ClientSecret={ClientSecretLength}, TenantId={TenantIdLength}",
                    clientId?.Length ?? 0, clientSecret?.Length ?? 0, tenantId?.Length ?? 0);

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(tenantId))
                {
                    _logger.LogWarning("Microsoft Graph configuration is missing. Using fallback mode.");
                    return null;
                }

                _logger.LogInformation("Creating ClientSecretCredential...");
                
                var options = new ClientSecretCredentialOptions
                {
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                };

                var clientSecretCredential = new ClientSecretCredential(
                    tenantId,
                    clientId,
                    clientSecret,
                    options);

                _logger.LogInformation("Creating GraphServiceClient...");
                var graphServiceClient = new GraphServiceClient(clientSecretCredential);
                
                _logger.LogInformation("GraphServiceClient created successfully");
                return graphServiceClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create GraphServiceClient");
                return null;
            }
        }

        public async Task<string> GetUserProfileAsync(string userEmail)
        {
            try
            {
                _logger.LogInformation("GetUserProfileAsync called for: {Email}", userEmail);

                // Check if we have real Graph API configuration
                var clientId = _configuration["GraphApi:ClientId"];
                var clientSecret = _configuration["GraphApi:ClientSecret"];
                var tenantId = _configuration["GraphApi:TenantId"];

                _logger.LogInformation("Configuration check: ClientId={HasClientId}, ClientSecret={HasClientSecret}, TenantId={HasTenantId}",
                    !string.IsNullOrEmpty(clientId), !string.IsNullOrEmpty(clientSecret), !string.IsNullOrEmpty(tenantId));

                bool hasRealConfig = !string.IsNullOrEmpty(clientId) && 
                                   !string.IsNullOrEmpty(clientSecret) && 
                                   !string.IsNullOrEmpty(tenantId);

                _logger.LogInformation("Has real config: {HasRealConfig}", hasRealConfig);

                if (hasRealConfig)
                {
                    _logger.LogInformation("Attempting to use real Microsoft Graph API");
                    
                    // Use the real Microsoft Graph API
                    var graphClient = CreateGraphServiceClient();
                    _logger.LogInformation("GraphClient created: {IsNotNull}", graphClient != null);
                    
                    if (graphClient != null)
                    {
                        try
                        {
                            _logger.LogInformation("Calling Microsoft Graph API for user: {Email}", userEmail);
                            var user = await graphClient.Users[userEmail].GetAsync();
                            _logger.LogInformation("Graph API call completed. User found: {UserFound}", user != null);
                            
                            if (user != null)
                            {
                                _logger.LogInformation("Creating user profile from Graph API response");
                                var userProfile = new
                                {
                                    Id = user.Id,
                                    DisplayName = user.DisplayName,
                                    Mail = user.Mail ?? userEmail,
                                    JobTitle = user.JobTitle,
                                    Department = user.Department,
                                    OfficeLocation = user.OfficeLocation,
                                    BusinessPhones = user.BusinessPhones?.ToArray(),
                                    MobilePhone = user.MobilePhone,
                                    UserPrincipalName = user.UserPrincipalName,
                                    CompanyName = user.CompanyName,
                                    Country = user.Country,
                                    City = user.City,
                                    State = user.State,
                                    CreatedDateTime = user.CreatedDateTime,
                                    Note = "Retrieved from Microsoft Graph API"
                                };
                                _logger.LogInformation("Returning real user data from Microsoft Graph API");
                                return JsonSerializer.Serialize(userProfile, new JsonSerializerOptions { WriteIndented = true });
                            }
                            else
                            {
                                _logger.LogWarning("User not found in Microsoft Graph API: {Email}", userEmail);
                            }
                        }
                        catch (Exception graphEx)
                        {
                            _logger.LogError(graphEx, "Error calling Microsoft Graph API for user {Email}", userEmail);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("GraphServiceClient is null, falling back to simulated data");
                    }
                }
                else
                {
                    _logger.LogInformation("No real config detected, using simulated data");
                }

                // Return simulated data for now
                _logger.LogInformation("Returning simulated user profile for: {Email}", userEmail);
                return GetSimulatedUserProfile(userEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user profile for {Email}. Falling back to simulated data.", userEmail);
                return GetSimulatedUserProfile(userEmail);
            }
        }

        public async Task<string> GetCurrentUserProfileAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving current user profile");

                // Check if we have real Graph API configuration
                var clientId = _configuration["GraphApi:ClientId"];
                var clientSecret = _configuration["GraphApi:ClientSecret"];
                var tenantId = _configuration["GraphApi:TenantId"];

                bool hasRealConfig = !string.IsNullOrEmpty(clientId) && 
                                   !string.IsNullOrEmpty(clientSecret) && 
                                   !string.IsNullOrEmpty(tenantId);

                if (hasRealConfig)
                {
                    // Use the real Microsoft Graph API
                    var graphClient = CreateGraphServiceClient();
                    if (graphClient != null)
                    {
                        try
                        {
                            // Get the first user as a demo (since service principal can't get "me")
                            var users = await graphClient.Users.GetAsync(requestConfiguration =>
                            {
                                requestConfiguration.QueryParameters.Top = 1;
                                requestConfiguration.QueryParameters.Select = new[] { "id", "displayName", "mail", "jobTitle", "department", "officeLocation", "businessPhones", "mobilePhone", "userPrincipalName", "companyName", "country", "city", "state", "createdDateTime" };
                            });

                            var user = users?.Value?.FirstOrDefault();
                            if (user != null)
                            {
                                var userProfile = new
                                {
                                    Id = user.Id,
                                    DisplayName = user.DisplayName,
                                    Mail = user.Mail,
                                    JobTitle = user.JobTitle,
                                    Department = user.Department,
                                    OfficeLocation = user.OfficeLocation,
                                    BusinessPhones = user.BusinessPhones?.ToArray(),
                                    MobilePhone = user.MobilePhone,
                                    UserPrincipalName = user.UserPrincipalName,
                                    CompanyName = user.CompanyName,
                                    Country = user.Country,
                                    City = user.City,
                                    State = user.State,
                                    CreatedDateTime = user.CreatedDateTime,
                                    Note = "Retrieved from Microsoft Graph API"
                                };
                                return JsonSerializer.Serialize(userProfile, new JsonSerializerOptions { WriteIndented = true });
                            }
                        }
                        catch (Exception graphEx)
                        {
                            _logger.LogError(graphEx, "Error calling Microsoft Graph API for current user");
                        }
                    }
                }

                // Return simulated data for now
                return GetSimulatedCurrentUserProfile();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current user profile. Falling back to simulated data.");
                return GetSimulatedCurrentUserProfile();
            }
        }

        private string GetSimulatedDisplayName(string email)
        {
            // Extract name from email for simulation
            var localPart = email.Split('@')[0];
            var parts = localPart.Split('.');
            if (parts.Length >= 2)
            {
                return $"{char.ToUpper(parts[0][0])}{parts[0][1..]} {char.ToUpper(parts[1][0])}{parts[1][1..]}";
            }
            return $"{char.ToUpper(localPart[0])}{localPart[1..]}";
        }

        private string GetSimulatedUserProfile(string userEmail)
        {
            _logger.LogInformation("GetSimulatedUserProfile called for: {Email}", userEmail);
            
            var simulatedProfile = new
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = GetSimulatedDisplayName(userEmail),
                Mail = userEmail,
                JobTitle = "Software Developer",
                Department = "Engineering",
                OfficeLocation = "Building A B C D, Floor 3",
                BusinessPhones = new[] { "+1-555-0123" },
                MobilePhone = "+1-555-0456",
                UserPrincipalName = userEmail,
                CompanyName = "Contoso Corporation",
                Country = "United States",
                City = "Seattle",
                State = "Washington",
                CreatedDateTime = DateTime.Now.AddYears(-2),
                LastSignInDateTime = DateTime.Now.AddHours(-2),
                Note = "Simulated data - Microsoft Graph API not configured"
            };

            return JsonSerializer.Serialize(simulatedProfile, new JsonSerializerOptions { WriteIndented = true });
        }

        private string GetSimulatedCurrentUserProfile()
        {
            var simulatedProfile = new
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "Current Test User",
                Mail = "testuser@contoso.com",
                JobTitle = "Senior Software Engineer",
                Department = "Engineering",
                OfficeLocation = "Building B, Floor 2",
                BusinessPhones = new[] { "+1-555-0789" },
                MobilePhone = "+1-555-0012",
                UserPrincipalName = "testuser@contoso.com",
                CompanyName = "Contoso Corporation",
                Country = "United States",
                City = "Redmond",
                State = "Washington",
                CreatedDateTime = DateTime.Now.AddYears(-3),
                LastSignInDateTime = DateTime.Now.AddMinutes(-15),
                Note = "Simulated data - Microsoft Graph API not configured"
            };

            return JsonSerializer.Serialize(simulatedProfile, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}