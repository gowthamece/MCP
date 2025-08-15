using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

namespace MCP_Balzor_AI_App.Services
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
            _mcpServerBaseUrl = "http://localhost:8080/mcp"; // MCP Server endpoint
        }

        public async Task<bool> ConnectToMCPServerAsync()
        {
            try
            {
                _logger.LogInformation("Connecting to MCP Server at {BaseUrl}", _mcpServerBaseUrl);
                
                // Try to ping the server
                var response = await _httpClient.GetAsync($"{_mcpServerBaseUrl}/health");
                
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

        public async Task<string> CallGetUserProfileToolAsync(string email)
        {
            try
            {
                _logger.LogInformation("Calling MCP Server get_user_profile tool for email: {Email}", email);

                var request = new
                {
                    tool = "get_user_profile",
                    arguments = new { email = email }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_mcpServerBaseUrl}/tools/call", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Successfully called MCP Server tool");
                    return responseContent;
                }
                else
                {
                    _logger.LogWarning("MCP Server tool call failed, falling back to simulation");
                    return await FallbackGetUserProfileAsync(email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling MCP Server tool, falling back to simulation");
                return await FallbackGetUserProfileAsync(email);
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

                var response = await _httpClient.PostAsync($"{_mcpServerBaseUrl}/tools/call", content);

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
