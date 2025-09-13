using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

namespace MCP.ADB2C.Client.Services
{
    public class HttpMCPClientService
    {
        private readonly ILogger<HttpMCPClientService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _mcpServerBaseUrl;

        public HttpMCPClientService(ILogger<HttpMCPClientService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _mcpServerBaseUrl = "http://localhost:5156"; // MCP.ADB2C Server endpoint
        }

        public async Task<bool> ConnectToMCPServerAsync()
        {
            try
            {
                _logger.LogInformation("Connecting to MCP Server at {BaseUrl}", _mcpServerBaseUrl);
                
                // Try to ping the server using the WeatherForecast endpoint as a health check
                var response = await _httpClient.GetAsync($"{_mcpServerBaseUrl}/WeatherForecast");
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully connected to MCP Server");
                    return true;
                }
                else
                {
                    _logger.LogWarning("MCP Server not available, falling back to simulation");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to MCP Server, falling back to simulation");
                return false;
            }
        }

        public async Task<string> CallGetUserProfileToolAsync(string email = "")
        {
            try
            {
                _logger.LogInformation("🚀 CALLING MCP Server GetADB2CUser tool to retrieve all Azure AD B2C users");

                // Call the User endpoint directly (GET /User)
                var requestUrl = $"{_mcpServerBaseUrl}/User";
                _logger.LogInformation("📡 REQUEST URL: {Url}", requestUrl);
                
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"📡 HTTP CLIENT REQUEST: {requestUrl}");
                Console.ResetColor();

                var response = await _httpClient.GetAsync(requestUrl);

                _logger.LogInformation("📨 RESPONSE STATUS: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                
                Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"📨 HTTP RESPONSE: {response.StatusCode} - {response.ReasonPhrase}");
                Console.ResetColor();

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("✅ SUCCESS: Retrieved {Length} characters from MCP Server", responseContent?.Length ?? 0);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ HTTP SUCCESS: {responseContent?.Length ?? 0} characters received");
                    Console.ResetColor();
                    
                    return responseContent ?? string.Empty;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("❌ MCP Server tool call failed with status {StatusCode}", response.StatusCode);
                    _logger.LogWarning("Error response: {ErrorContent}", errorContent);
                    
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ HTTP ERROR {response.StatusCode}: {errorContent}");
                    Console.ResetColor();
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("🔒 UNAUTHORIZED - Check authentication configuration");
                        Console.ResetColor();
                    }
                    
                    _logger.LogInformation("🔄 FALLING BACK TO SIMULATION");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("🔄 FALLING BACK TO SIMULATION");
                    Console.ResetColor();
                    
                    return await FallbackGetUserProfileAsync("");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR calling MCP Server tool");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 HTTP CLIENT ERROR: {ex.Message}");
                Console.ResetColor();
                
                _logger.LogInformation("🔄 FALLING BACK TO SIMULATION due to exception");
                return await FallbackGetUserProfileAsync("");
            }
        }

        public async Task<string> CallGetCurrentUserProfileToolAsync()
        {
            try
            {
                _logger.LogInformation("Calling MCP Server get_current_user_profile tool");

                var request = new
                {
                    tool = "get_current_user_profile",
                    arguments = new { }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_mcpServerBaseUrl}/api/tools/execute", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Successfully called MCP Server tool");
                    return responseContent;
                }
                else
                {
                    _logger.LogWarning("MCP Server tool call failed, falling back to simulation");
                    return await FallbackGetCurrentUserProfileAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling MCP Server tool, falling back to simulation");
                return await FallbackGetCurrentUserProfileAsync();
            }
        }

        private async Task<string> FallbackGetUserProfileAsync(string email)
        {
            await Task.Delay(300); // Simulate network call
            
            // If no email provided, return all users
            if (string.IsNullOrEmpty(email))
            {
                var allUsers = new[]
                {
                    new
                    {
                        Id = Guid.NewGuid().ToString(),
                        DisplayName = "John Doe",
                        Mail = "john.doe@contoso.com",
                        JobTitle = "Software Developer",
                        Department = "Engineering",
                        OfficeLocation = "Building A, Floor 3",
                        BusinessPhones = new[] { "+1-555-0123" },
                        MobilePhone = "+1-555-0456",
                        UserPrincipalName = "john.doe@contoso.com",
                        CompanyName = "Contoso Corporation (Simulated)",
                        Country = "United States",
                        City = "Seattle",
                        State = "Washington"
                    },
                    new
                    {
                        Id = Guid.NewGuid().ToString(),
                        DisplayName = "Jane Smith",
                        Mail = "jane.smith@contoso.com",
                        JobTitle = "Product Manager",
                        Department = "Product",
                        OfficeLocation = "Building B, Floor 2",
                        BusinessPhones = new[] { "+1-555-0789" },
                        MobilePhone = "+1-555-0012",
                        UserPrincipalName = "jane.smith@contoso.com",
                        CompanyName = "Contoso Corporation (Simulated)",
                        Country = "United States",
                        City = "Redmond",
                        State = "Washington"
                    },
                    new
                    {
                        Id = Guid.NewGuid().ToString(),
                        DisplayName = "Mike Johnson",
                        Mail = "mike.johnson@contoso.com",
                        JobTitle = "DevOps Engineer",
                        Department = "Engineering",
                        OfficeLocation = "Building A, Floor 1",
                        BusinessPhones = new[] { "+1-555-0345" },
                        MobilePhone = "+1-555-0678",
                        UserPrincipalName = "mike.johnson@contoso.com",
                        CompanyName = "Contoso Corporation (Simulated)",
                        Country = "United States",
                        City = "Seattle",
                        State = "Washington"
                    }
                };

                return JsonSerializer.Serialize(allUsers, new JsonSerializerOptions { WriteIndented = true });
            }
            
            // Single user profile for specific email
            var profile = new
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = GetDisplayNameFromEmail(email),
                Mail = email,
                JobTitle = "Software Developer",
                Department = "Engineering",
                OfficeLocation = "Building A B, Floor 3",
                BusinessPhones = new[] { "+1-555-0123" },
                MobilePhone = "+1-555-0456",
                UserPrincipalName = email,
                CompanyName = "Contoso Corporation (Simulated)",
                Country = "United States",
                City = "Seattle",
                State = "Washington"
            };

            return JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        }

        private async Task<string> FallbackGetCurrentUserProfileAsync()
        {
            await Task.Delay(300); // Simulate network call
            
            var profile = new
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "Current User",
                Mail = "currentuser@contoso.com",
                JobTitle = "Senior Software Engineer",
                Department = "Engineering",
                OfficeLocation = "Building B, Floor 2",
                BusinessPhones = new[] { "+1-555-0789" },
                MobilePhone = "+1-555-0012",
                UserPrincipalName = "currentuser@contoso.com",
                CompanyName = "Contoso Corporation (Simulated)"
            };

            return JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        }

        // New methods for all MCP tools
        public async Task<string> CallGetUsersToolAsync()
        {
            return await CallGetUserProfileToolAsync(""); // Reuse existing method for all users
        }

        public async Task<string> CallGetUsersByAppRoleToolAsync(string appRole, string appName)
        {
            try
            {
                _logger.LogInformation("Calling MCP Server GetADB2CUsersByAppRole tool with appRole: {AppRole}, appName: {AppName}", appRole, appName);

                var response = await _httpClient.GetAsync($"{_mcpServerBaseUrl}/User/by-app-role?appRole={Uri.EscapeDataString(appRole)}&appName={Uri.EscapeDataString(appName)}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Successfully called MCP Server GetADB2CUsersByAppRole tool");
                    return responseContent;
                }
                else
                {
                    _logger.LogWarning("MCP Server tool call failed with status {StatusCode}, falling back to simulation", response.StatusCode);
                    return await FallbackGetUsersByAppRoleAsync(appRole, appName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling MCP Server GetADB2CUsersByAppRole tool, falling back to simulation");
                return await FallbackGetUsersByAppRoleAsync(appRole, appName);
            }
        }

        public async Task<string> CallGetB2CApplicationsToolAsync(bool ownedOnly = false)
        {
            try
            {
                _logger.LogInformation("Calling MCP Server GetB2CApplications tool with ownedOnly: {OwnedOnly}", ownedOnly);

                var response = await _httpClient.GetAsync($"{_mcpServerBaseUrl}/Application?ownedOnly={ownedOnly}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Successfully called MCP Server GetB2CApplications tool");
                    return responseContent;
                }
                else
                {
                    _logger.LogWarning("MCP Server tool call failed with status {StatusCode}, falling back to simulation", response.StatusCode);
                    return ownedOnly ? await FallbackGetApplicationsIOwnedAsync() : await FallbackGetApplicationsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling MCP Server GetB2CApplications tool, falling back to simulation");
                return ownedOnly ? await FallbackGetApplicationsIOwnedAsync() : await FallbackGetApplicationsAsync();
            }
        }

        public async Task<string> CallGetApplicationRolesToolAsync(string appName)
        {
            try
            {
                _logger.LogInformation("Calling MCP Server GetADB2CApplicationRole tool with appName: {AppName}", appName);

                var response = await _httpClient.GetAsync($"{_mcpServerBaseUrl}/Roles?appName={Uri.EscapeDataString(appName)}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Successfully called MCP Server GetADB2CApplicationRole tool");
                    return responseContent;
                }
                else
                {
                    _logger.LogWarning("MCP Server tool call failed with status {StatusCode}, falling back to simulation", response.StatusCode);
                    return await FallbackGetApplicationRolesAsync(appName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling MCP Server GetADB2CApplicationRole tool, falling back to simulation");
                return await FallbackGetApplicationRolesAsync(appName);
            }
        }

        public async Task<string> CallAssignRoleToUserToolAsync(string username, string appName, string roleName)
        {
            try
            {
                _logger.LogInformation("Calling MCP Server AssignRoletoUser tool with username: {Username}, appName: {AppName}, roleName: {RoleName}", username, appName, roleName);

                var response = await _httpClient.GetAsync($"{_mcpServerBaseUrl}/Roles/assign?username={Uri.EscapeDataString(username)}&appName={Uri.EscapeDataString(appName)}&roleName={Uri.EscapeDataString(roleName)}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Successfully called MCP Server AssignRoletoUser tool");
                    return responseContent;
                }
                else
                {
                    _logger.LogWarning("MCP Server tool call failed with status {StatusCode}, falling back to simulation", response.StatusCode);
                    return await FallbackAssignRoleToUserAsync(username, appName, roleName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling MCP Server AssignRoletoUser tool, falling back to simulation");
                return await FallbackAssignRoleToUserAsync(username, appName, roleName);
            }
        }

        public async Task<string> CallGetWeatherForecastToolAsync()
        {
            try
            {
                _logger.LogInformation("Calling MCP Server GetWeatherAuthAPI tool");

                var response = await _httpClient.GetAsync($"{_mcpServerBaseUrl}/WeatherForecast");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Successfully called MCP Server GetWeatherAuthAPI tool");
                    return responseContent;
                }
                else
                {
                    _logger.LogWarning("MCP Server tool call failed with status {StatusCode}, falling back to simulation", response.StatusCode);
                    return await FallbackGetWeatherForecastAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling MCP Server GetWeatherAuthAPI tool, falling back to simulation");
                return await FallbackGetWeatherForecastAsync();
            }
        }

        // Fallback methods for simulation
        private async Task<string> FallbackGetUsersByAppRoleAsync(string appRole, string appName)
        {
            await Task.Delay(300);
            
            var users = new[]
            {
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = "App User 1",
                    Email = "appuser1@contoso.com",
                    Type = "User",
                    AppRole = appRole,
                    AppName = appName
                },
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = "App User 2",
                    Email = "appuser2@contoso.com",
                    Type = "User",
                    AppRole = appRole,
                    AppName = appName
                }
            };

            return JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
        }

        private async Task<string> FallbackGetApplicationsAsync()
        {
            await Task.Delay(300);
            
            var applications = new[]
            {
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    appId = Guid.NewGuid().ToString(),
                    Name = "Sample App 1 (Simulated)",
                    CreatedDateTime = DateTime.UtcNow.AddDays(-30)
                },
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    appId = Guid.NewGuid().ToString(),
                    Name = "Sample App 2 (Simulated)",
                    CreatedDateTime = DateTime.UtcNow.AddDays(-15)
                }
            };

            return JsonSerializer.Serialize(applications, new JsonSerializerOptions { WriteIndented = true });
        }

        private async Task<string> FallbackGetApplicationsIOwnedAsync()
        {
            await Task.Delay(300);
            
            var applications = new[]
            {
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    appId = Guid.NewGuid().ToString(),
                    Name = "My App 1 (Simulated)",
                    CreatedDateTime = DateTime.UtcNow.AddDays(-10),
                    OwnedByCurrentUser = true
                }
            };

            return JsonSerializer.Serialize(applications, new JsonSerializerOptions { WriteIndented = true });
        }

        private async Task<string> FallbackGetApplicationRolesAsync(string appName)
        {
            await Task.Delay(300);
            
            var roles = new[]
            {
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Admin",
                    AppId = Guid.NewGuid().ToString(),
                    AppName = appName
                },
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "User",
                    AppId = Guid.NewGuid().ToString(),
                    AppName = appName
                }
            };

            return JsonSerializer.Serialize(roles, new JsonSerializerOptions { WriteIndented = true });
        }

        private async Task<string> FallbackAssignRoleToUserAsync(string username, string appName, string roleName)
        {
            await Task.Delay(300);
            
            var result = new
            {
                Success = true,
                Message = $"Assigned {roleName} to user {username} for application {appName} (Simulated)",
                Username = username,
                AppName = appName,
                RoleName = roleName,
                AssignedAt = DateTime.UtcNow
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }

        private async Task<string> FallbackGetWeatherForecastAsync()
        {
            await Task.Delay(300);
            
            var weather = Enumerable.Range(1, 5).Select(index => new
            {
                Date = DateTime.Now.AddDays(index).ToString("yyyy-MM-dd"),
                TemperatureC = Random.Shared.Next(-20, 55),
                TemperatureF = Random.Shared.Next(-4, 131),
                Summary = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" }[Random.Shared.Next(10)]
            });

            return JsonSerializer.Serialize(weather, new JsonSerializerOptions { WriteIndented = true });
        }

        private string GetDisplayNameFromEmail(string email)
        {
            var localPart = email.Split('@')[0];
            var parts = localPart.Split('.');
            if (parts.Length >= 2)
            {
                return $"{char.ToUpper(parts[0][0])}{parts[0][1..]} {char.ToUpper(parts[1][0])}{parts[1][1..]}";
            }
            return $"{char.ToUpper(localPart[0])}{localPart[1..]}";
        }
    }
}
