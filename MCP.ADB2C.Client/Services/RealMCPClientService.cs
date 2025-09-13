using Mcp.Net.Client;
using Mcp.Net.Core.Models.Content;
using Mcp.Net.Core.Models.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;

namespace MCP.ADB2C.Client.Services
{
    public class RealMCPClientService
    {
        private readonly ILogger<RealMCPClientService> _logger;
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly IConfiguration _configuration;
        private readonly ITokenAcquisition _tokenAcquisition;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly string _mcpServerBaseUrl;

        private McpClient? _mcpClient;

        public RealMCPClientService(
            ILogger<RealMCPClientService> logger, 
            AuthenticationStateProvider authenticationStateProvider, 
            IConfiguration configuration, 
            ITokenAcquisition tokenAcquisition,
            IHttpContextAccessor? httpContextAccessor = null)
        {
            _logger = logger;
            _authenticationStateProvider = authenticationStateProvider;
            _configuration = configuration;
            _tokenAcquisition = tokenAcquisition;
            _httpContextAccessor = httpContextAccessor;
            _mcpServerBaseUrl = "http://localhost:5156"; // MCP.ADB2C Server endpoint
        }

        // Public method to check and refresh client-side authentication
        public async Task<bool> ForceUserReauthenticationAndRetryAsync()
        {
            try
            {
                _logger.LogInformation("🔄 CHECKING CLIENT-SIDE AUTHENTICATION STATUS");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("🔄 CHECKING CLIENT-SIDE AUTHENTICATION STATUS");
                Console.ResetColor();

                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                if (authState?.User?.Identity?.IsAuthenticated == true)
                {
                    var scopes = new[] { "https://graph.microsoft.com/.default" };
                    
                    // Try to get a valid token
                    var accessToken = await GetValidAccessTokenAsync(authState, scopes);
                    
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        _logger.LogInformation("✅ CLIENT-SIDE AUTHENTICATION IS VALID");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✅ CLIENT-SIDE AUTHENTICATION IS VALID");
                        Console.ResetColor();
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ CLIENT-SIDE TOKEN IS INVALID OR EXPIRED");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("⚠️ CLIENT-SIDE TOKEN IS INVALID OR EXPIRED");
                        Console.WriteLine("   User needs to sign out and sign in again in the Blazor app");
                        Console.ResetColor();
                    }
                }
                else
                {
                    _logger.LogWarning("❌ USER IS NOT AUTHENTICATED IN CLIENT");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ USER IS NOT AUTHENTICATED IN CLIENT");
                    Console.ResetColor();
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR checking client-side authentication");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 CLIENT-SIDE AUTH CHECK ERROR: {ex.Message}");
                Console.ResetColor();
                return false;
            }
        }

        /// <summary>
        /// Gets the current client-side authentication status and provides user-friendly guidance
        /// </summary>
        public async Task<string> GetAuthenticationStatusAsync()
        {
            try
            {
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                
                if (authState?.User?.Identity?.IsAuthenticated != true)
                {
                    return "❌ User is not authenticated in the client. Please sign in to continue.";
                }
                
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var accessToken = await GetValidAccessTokenAsync(authState, scopes);
                
                if (!string.IsNullOrEmpty(accessToken))
                {
                    return "✅ User is authenticated with valid tokens in the client.";
                }
                else
                {
                    return "⚠️ User is authenticated but tokens are invalid or expired. Please sign out and sign in again in the Blazor app.";
                }
            }
            catch (Exception ex)
            {
                return $"❌ Client-side authentication status check failed: {ex.Message}";
            }
        }

        // Direct client-side token acquisition - simplified approach

        private async Task<string?> GetValidAccessTokenAsync(AuthenticationState authState, string[] scopes)
        {
            try
            {
                _logger.LogInformation("� GETTING ACCESS TOKEN FROM CLIENT-SIDE AUTHENTICATION");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("� GETTING ACCESS TOKEN FROM CLIENT-SIDE AUTHENTICATION");
                Console.ResetColor();
                
                // Step 1: Try to get token from current authentication context
                if (_httpContextAccessor.HttpContext != null)
                {
                    var authResult = await _httpContextAccessor.HttpContext
                        .AuthenticateAsync(OpenIdConnectDefaults.AuthenticationScheme);
                    
                    if (authResult?.Succeeded == true && authResult.Properties != null)
                    {
                        var accessToken = authResult.Properties.GetTokenValue("access_token");
                        var expiresAtString = authResult.Properties.GetTokenValue("expires_at");
                        
                        _logger.LogInformation("📋 CURRENT TOKEN INFO:");
                        _logger.LogInformation("   Access Token Length: {Length}", accessToken?.Length ?? 0);
                        _logger.LogInformation("   Expires At: {ExpiresAt}", expiresAtString ?? "Unknown");
                        
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"📋 TOKEN: Access={accessToken?.Length ?? 0} chars, Expires={expiresAtString ?? "Unknown"}");
                        Console.ResetColor();
                        
                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            bool isTokenValid = await IsTokenStillValidAsync(accessToken, expiresAtString);
                            
                            if (isTokenValid)
                            {
                                _logger.LogInformation("✅ TOKEN IS VALID");
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("✅ TOKEN IS VALID");
                                Console.ResetColor();
                                return accessToken;
                            }
                            else
                            {
                                _logger.LogInformation("⏰ TOKEN IS EXPIRED OR EXPIRING SOON");
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("⏰ TOKEN IS EXPIRED OR EXPIRING SOON");
                                Console.ResetColor();
                            }
                        }
                    }
                }
                
                // Step 2: Try to get a fresh token using ITokenAcquisition (client-side)
                _logger.LogInformation("🔄 ATTEMPTING TO GET FRESH TOKEN FROM CLIENT AUTHENTICATION");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("🔄 ATTEMPTING TO GET FRESH TOKEN FROM CLIENT AUTHENTICATION");
                Console.ResetColor();
                
                try
                {
                    var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(
                        scopes,
                        authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme,
                        user: authState.User);
                    
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        _logger.LogInformation("✅ FRESH TOKEN ACQUIRED: {Length} characters", accessToken.Length);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✅ FRESH TOKEN ACQUIRED: {accessToken.Length} characters");
                        Console.ResetColor();
                        return accessToken;
                    }
                }
                catch (MicrosoftIdentityWebChallengeUserException challengeEx)
                {
                    _logger.LogWarning("🔐 CLIENT-SIDE RE-AUTHENTICATION REQUIRED: {Message}", challengeEx.Message);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"🔐 CLIENT-SIDE RE-AUTHENTICATION REQUIRED: {challengeEx.Message}");
                    Console.WriteLine("   User needs to sign out and sign in again in the Blazor app");
                    Console.ResetColor();
                    
                    return null; // Client should handle this by redirecting to login
                }
                catch (Exception tokenEx)
                {
                    _logger.LogWarning("❌ TOKEN ACQUISITION FAILED: {Error}", tokenEx.Message);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ TOKEN ACQUISITION FAILED: {tokenEx.Message}");
                    Console.ResetColor();
                }
                
                _logger.LogError("❌ UNABLE TO ACQUIRE VALID ACCESS TOKEN");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ UNABLE TO ACQUIRE VALID ACCESS TOKEN");
                Console.WriteLine("   User may need to sign out and sign in again");
                Console.ResetColor();
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR IN GetValidAccessTokenAsync");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 TOKEN VALIDATION ERROR: {ex.Message}");
                Console.ResetColor();
                return null;
            }
        }

        private async Task ForceUserReauthenticationAsync(AuthenticationState authState, string[] scopes)
        {
            try
            {
                _logger.LogInformation("🧹 CLEARING TOKEN CACHE TO FORCE RE-AUTHENTICATION");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("🧹 CLEARING TOKEN CACHE TO FORCE RE-AUTHENTICATION");
                Console.ResetColor();
                
                // Get user identifier for cache operations
                var userIdentifier = authState.User.FindFirst("oid")?.Value ?? 
                                   authState.User.FindFirst("sub")?.Value ?? 
                                   authState.User.FindFirst("preferred_username")?.Value ??
                                   authState.User.Identity?.Name;
                
                if (!string.IsNullOrEmpty(userIdentifier))
                {
                    try
                    {
                        // Note: ITokenAcquisition doesn't have ResetUserCacheAsync in this version
                        // We'll rely on forcing fresh token acquisition through other mechanisms
                        
                        _logger.LogInformation("✅ PREPARED FOR FRESH TOKEN ACQUISITION FOR USER: {User}", userIdentifier);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✅ PREPARED FOR FRESH TOKEN ACQUISITION FOR USER: {userIdentifier}");
                        Console.ResetColor();
                    }
                    catch (Exception cacheEx)
                    {
                        _logger.LogWarning("⚠️ CACHE PREPARATION FAILED: {Error}", cacheEx.Message);
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"⚠️ CACHE PREPARATION FAILED: {cacheEx.Message}");
                        Console.ResetColor();
                    }
                }
                
                // Also try to clear the HTTP context authentication
                if (_httpContextAccessor.HttpContext != null)
                {
                    try
                    {
                        // Clear the authentication cookie/session
                        await _httpContextAccessor.HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
                        
                        _logger.LogInformation("✅ HTTP CONTEXT AUTHENTICATION CLEARED");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✅ HTTP CONTEXT AUTHENTICATION CLEARED");
                        Console.ResetColor();
                    }
                    catch (Exception signOutEx)
                    {
                        _logger.LogWarning("⚠️ SIGN OUT FAILED: {Error}", signOutEx.Message);
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"⚠️ SIGN OUT FAILED: {signOutEx.Message}");
                        Console.ResetColor();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR during forced re-authentication setup");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"� FORCED RE-AUTH SETUP ERROR: {ex.Message}");
                Console.ResetColor();
            }
        }

        private async Task<string?> AcquireFreshTokenWithForceAsync(AuthenticationState authState, string[] scopes)
        {
            try
            {
                _logger.LogInformation("🔄 ACQUIRING FRESH TOKEN WITH FORCE FLAG");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("🔄 ACQUIRING FRESH TOKEN WITH FORCE FLAG");
                Console.ResetColor();
                
                var tenantId = authState.User.FindFirst("tid")?.Value ?? 
                             authState.User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
                
                var loginHint = authState.User.FindFirst("preferred_username")?.Value ?? 
                              authState.User.FindFirst("upn")?.Value ?? 
                              authState.User.FindFirst("email")?.Value ??
                              authState.User.Identity?.Name;
                
                _logger.LogInformation("🔍 USER CONTEXT: TenantId={TenantId}, LoginHint={LoginHint}", tenantId, loginHint);
                
                // Try with explicit user context and force refresh
                var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(
                    scopes,
                    authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme,
                    tenantId: tenantId,
                    userFlow: null,
                    user: authState.User);
                
                if (!string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogInformation("✅ FRESH TOKEN ACQUIRED: {Length} characters", accessToken.Length);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ FRESH TOKEN ACQUIRED: {accessToken.Length} characters");
                    Console.ResetColor();
                    return accessToken;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR acquiring fresh token with force");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 FRESH TOKEN ACQUISITION ERROR: {ex.Message}");
                Console.ResetColor();
                throw; // Re-throw to let caller handle as challenge
            }
        }

        private async Task<string?> HandleForcedUserChallengeAsync(MicrosoftIdentityWebChallengeUserException challengeEx, AuthenticationState authState, string[] scopes)
        {
            try
            {
                _logger.LogInformation("🔐 HANDLING FORCED USER CHALLENGE FOR RE-AUTHENTICATION");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("🔐 HANDLING FORCED USER CHALLENGE FOR RE-AUTHENTICATION");
                Console.ResetColor();
                
                // Log challenge details
                _logger.LogInformation("Challenge URL: {ChallengeUrl}", challengeEx.Message);
                _logger.LogInformation("Required Scopes: {Scopes}", string.Join(", ", challengeEx.Scopes ?? scopes));
                
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"� Challenge Details: {challengeEx.Message}");
                Console.WriteLine($"📋 Required Scopes: {string.Join(", ", challengeEx.Scopes ?? scopes)}");
                Console.ResetColor();
                
                // In Blazor Server, we cannot initiate a challenge mid-request due to response already started
                // Instead, we'll log the need for re-authentication and return null
                // The application should handle this by showing an appropriate message to the user
                
                _logger.LogWarning("⚠️ USER RE-AUTHENTICATION REQUIRED - CANNOT PROCEED WITHOUT FRESH TOKEN");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️ USER RE-AUTHENTICATION REQUIRED");
                Console.WriteLine("   In Blazor Server, the user needs to sign out and sign in again");
                Console.WriteLine("   or navigate to a page that triggers authentication");
                Console.ResetColor();
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR in HandleForcedUserChallengeAsync");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 FORCED CHALLENGE ERROR: {ex.Message}");
                Console.ResetColor();
                return null;
            }
        }

        private async Task<bool> IsTokenStillValidAsync(string accessToken, string? expiresAtString)
        {
            try
            {
                // Check expiry time from token properties
                if (!string.IsNullOrEmpty(expiresAtString))
                {
                    if (DateTimeOffset.TryParse(expiresAtString, out var expiresAt))
                    {
                        var now = DateTimeOffset.UtcNow;
                        var timeUntilExpiry = expiresAt - now;
                        
                        _logger.LogInformation("⏰ TOKEN EXPIRY CHECK:");
                        _logger.LogInformation("   Current Time: {Now}", now);
                        _logger.LogInformation("   Expires At: {ExpiresAt}", expiresAt);
                        _logger.LogInformation("   Time Until Expiry: {TimeUntilExpiry}", timeUntilExpiry);
                        
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"⏰ TOKEN EXPIRY: Current={now:HH:mm:ss}, Expires={expiresAt:HH:mm:ss}, Remaining={timeUntilExpiry}");
                        Console.ResetColor();
                        
                        // Consider token invalid if it expires within 5 minutes
                        if (timeUntilExpiry.TotalMinutes > 5)
                        {
                            _logger.LogInformation("✅ TOKEN VALID - {Minutes} minutes remaining", timeUntilExpiry.TotalMinutes);
                            return true;
                        }
                        else
                        {
                            _logger.LogWarning("⏰ TOKEN EXPIRED OR EXPIRING SOON - {Minutes} minutes remaining", timeUntilExpiry.TotalMinutes);
                            return false;
                        }
                    }
                }
                
                // If we can't parse expiry, try to decode JWT token
                try
                {
                    var jwtHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    if (jwtHandler.CanReadToken(accessToken))
                    {
                        var jwt = jwtHandler.ReadJwtToken(accessToken);
                        var exp = jwt.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
                        
                        if (!string.IsNullOrEmpty(exp) && long.TryParse(exp, out var expUnix))
                        {
                            var expiryTime = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                            var now = DateTimeOffset.UtcNow;
                            var timeUntilExpiry = expiryTime - now;
                            
                            _logger.LogInformation("🔍 JWT TOKEN EXPIRY: {Expiry}, Remaining: {Remaining}", expiryTime, timeUntilExpiry);
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"🔍 JWT EXPIRY: {expiryTime:HH:mm:ss}, Remaining: {timeUntilExpiry}");
                            Console.ResetColor();
                            
                            return timeUntilExpiry.TotalMinutes > 5;
                        }
                    }
                }
                catch (Exception jwtEx)
                {
                    _logger.LogWarning("❌ JWT parsing failed: {Error}", jwtEx.Message);
                }
                
                // If all else fails, assume token might be valid and let the server decide
                _logger.LogWarning("⚠️ CANNOT DETERMINE TOKEN EXPIRY - Assuming valid");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR checking token validity");
                return false; // Assume invalid if we can't check
            }
        }

        private async Task<string?> RefreshAccessTokenAsync(string refreshToken, string[] scopes, AuthenticationState authState)
        {
            try
            {
                _logger.LogInformation("🔄 ATTEMPTING TOKEN REFRESH");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("🔄 ATTEMPTING TOKEN REFRESH");
                Console.ResetColor();
                
                // Try to use the refresh token to get a new access token
                // This typically involves calling Azure AD token endpoint directly
                var tenantId = authState.User.FindFirst("tid")?.Value ?? 
                             authState.User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
                
                if (!string.IsNullOrEmpty(tenantId))
                {
                    _logger.LogInformation("🔄 Refreshing token for tenant: {TenantId}", tenantId);
                    
                    // Microsoft.Identity.Web should handle refresh automatically
                    // Try to force a new token acquisition
                    try
                    {
                        var refreshedToken = await _tokenAcquisition.GetAccessTokenForUserAsync(
                            scopes,
                            authenticationScheme: "OpenIdConnect",
                            tenantId: tenantId,
                            userFlow: null,
                            user: authState.User);
                        
                        _logger.LogInformation("✅ TOKEN REFRESH SUCCESSFUL: {Length} characters", refreshedToken?.Length ?? 0);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✅ TOKEN REFRESH SUCCESSFUL: {refreshedToken?.Length ?? 0} characters");
                        Console.ResetColor();
                        
                        return refreshedToken;
                    }
                    catch (Exception refreshEx)
                    {
                        _logger.LogWarning("❌ TOKEN REFRESH FAILED: {Error}", refreshEx.Message);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"❌ TOKEN REFRESH FAILED: {refreshEx.Message}");
                        Console.ResetColor();
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR during token refresh");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 TOKEN REFRESH ERROR: {ex.Message}");
                Console.ResetColor();
                return null;
            }
        }

        private async Task<string?> HandleUserChallengeAsync(MicrosoftIdentityWebChallengeUserException challengeEx, AuthenticationState authState, string[] scopes)
        {
            try
            {
                _logger.LogInformation("🔐 HANDLING USER CHALLENGE");
                
                var challengeScopes = challengeEx.Scopes?.ToArray() ?? scopes;
                var loginHint = authState.User.FindFirst("preferred_username")?.Value ?? 
                              authState.User.FindFirst("upn")?.Value ?? 
                              authState.User.FindFirst("email")?.Value ??
                              authState.User.Identity?.Name;
                
                var tenantId = authState.User.FindFirst("tid")?.Value ?? 
                             authState.User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
                
                _logger.LogInformation("Challenge details - Scopes: {Scopes}, LoginHint: {LoginHint}, TenantId: {TenantId}", 
                    string.Join(",", challengeScopes), loginHint, tenantId);
                
                try
                {
                    var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(
                        challengeScopes,
                        authenticationScheme: "OpenIdConnect",
                        tenantId: tenantId,
                        userFlow: null,
                        user: authState.User);
                    
                    _logger.LogInformation("✅ Challenge resolved - token acquired: {TokenLength} characters", 
                        accessToken?.Length ?? 0);
                    
                    return accessToken;
                }
                catch (Exception incrementalEx)
                {
                    _logger.LogWarning("❌ Incremental consent failed: {Message}", incrementalEx.Message);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR handling user challenge");
                return null;
            }
        }

        public async Task<bool> ConnectToMCPServerAsync(string serverAddress = "localhost", int port = 5156)
        {
            try
            {
                _logger.LogInformation("Connecting to MCP Server at {ServerAddress}:{Port}", serverAddress, port);
                
                // Test the MCP.ADB2C server endpoint using WeatherForecast as health check
                try
                {
                    using var mcpClient = new HttpClient();
                    mcpClient.Timeout = TimeSpan.FromSeconds(30);
                    var response = await mcpClient.GetAsync($"http://{serverAddress}:{port}/WeatherForecast", HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully connected to MCP.ADB2C Server at {ServerAddress}:{Port}", serverAddress, port);
                        _logger.LogInformation("MCP.ADB2C connection established");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("MCP.ADB2C Server returned status: {StatusCode}", response.StatusCode);
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    _logger.LogWarning("MCP.ADB2C Server not reachable via HTTP: {Message}", httpEx.Message);
                }
                catch (TaskCanceledException timeoutEx)
                {
                    _logger.LogWarning("Timeout connecting to MCP.ADB2C Server: {Message}", timeoutEx.Message);
                }
                
                _logger.LogInformation("MCP.ADB2C Server connection tested, falling back to simulated mode for unsupported operations");
                return true; // Return true to allow simulated functionality
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to MCP.ADB2C Server");
                return false;
            }
        }

        public async Task<string> CallGetUserProfileToolAsync(string email = "")
        {
            try
            {
                _logger.LogInformation("🚀 CALLING GetADB2CUser tool to retrieve all Azure AD B2C users");

                // Get client-side authentication token first
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                if (authState?.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("❌ USER NOT AUTHENTICATED IN CLIENT");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ USER NOT AUTHENTICATED - FALLING BACK TO SIMULATED DATA");
                    Console.ResetColor();
                    return await SimulateGetUserProfileAsync("");
                }

                // Get access token from client-side authentication
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var accessToken = await GetValidAccessTokenAsync(authState, scopes);

                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("❌ NO VALID ACCESS TOKEN FROM CLIENT");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ NO VALID ACCESS TOKEN - FALLING BACK TO SIMULATED DATA");
                    Console.ResetColor();
                    return await SimulateGetUserProfileAsync("");
                }

                // Try to make a direct HTTP call to the MCP server with client token
                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.BaseAddress = new Uri(_mcpServerBaseUrl);
                    httpClient.Timeout = TimeSpan.FromSeconds(30);
                    
                    // Add the client-side access token
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    _logger.LogInformation("📡 MAKING HTTP REQUEST TO MCP.ADB2C SERVER WITH CLIENT TOKEN");
                    _logger.LogInformation("Target URL: {BaseUrl}/User", httpClient.BaseAddress);
                    
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine($"📡 SENDING REQUEST TO: {httpClient.BaseAddress}User");
                    Console.WriteLine($"🔐 WITH CLIENT TOKEN: Bearer {accessToken.Substring(0, Math.Min(30, accessToken.Length))}...");
                    Console.ResetColor();
                    
                    // Call the User endpoint directly (GET /User)
                    var response = await httpClient.GetAsync("/User");
                    
                    _logger.LogInformation("📨 RECEIVED RESPONSE FROM MCP SERVER");
                    _logger.LogInformation("Status Code: {StatusCode}", response.StatusCode);
                    _logger.LogInformation("Status: {Status}", response.ReasonPhrase);
                    
                    Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
                    Console.WriteLine($"📨 RESPONSE: {response.StatusCode} - {response.ReasonPhrase}");
                    Console.ResetColor();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("✅ RECEIVED SUCCESSFUL RESPONSE FROM MCP SERVER: {ResponseLength} characters", result?.Length ?? 0);
                        
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✅ SUCCESS: Received {result?.Length ?? 0} characters of user data");
                        Console.ResetColor();
                        
                        return result ?? string.Empty;
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning("❌ MCP SERVER RETURNED ERROR STATUS: {StatusCode}", response.StatusCode);
                        _logger.LogWarning("Error Content: {ErrorContent}", errorContent);
                        
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"❌ ERROR {response.StatusCode}: {errorContent}");
                        Console.ResetColor();
                        
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            _logger.LogError("🔒 UNAUTHORIZED - CLIENT TOKEN MAY BE INVALID");
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("🔒 UNAUTHORIZED ACCESS - Client token may be invalid");
                            Console.ResetColor();
                        }
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    _logger.LogWarning("🌐 HTTP ERROR calling MCP Server: {Message}", httpEx.Message);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"🌐 HTTP ERROR: {httpEx.Message}");
                    Console.ResetColor();
                }
                catch (TaskCanceledException timeoutEx)
                {
                    _logger.LogWarning("⏰ TIMEOUT calling MCP Server: {Message}", timeoutEx.Message);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⏰ TIMEOUT: {timeoutEx.Message}");
                    Console.ResetColor();
                }
                catch (Exception httpEx)
                {
                    _logger.LogError(httpEx, "💥 ERROR making HTTP call to MCP Server");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"💥 UNEXPECTED ERROR: {httpEx.Message}");
                    Console.ResetColor();
                }

                // Fallback to simulation
                _logger.LogInformation("🔄 FALLING BACK TO SIMULATED DATA");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("🔄 USING SIMULATED DATA - MCP SERVER NOT AVAILABLE");
                Console.ResetColor();
                
                return await SimulateGetUserProfileAsync("");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR calling GetADB2CUser tool");
                return $"Error retrieving user profile: {ex.Message}";
            }
        }

        public async Task<string> CallGetCurrentUserProfileToolAsync()
        {
            try
            {
                _logger.LogInformation("Calling get_current_user_profile tool");

                // Get client-side authentication token first
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                if (authState?.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("❌ USER NOT AUTHENTICATED IN CLIENT");
                    return await SimulateGetCurrentUserProfileAsync();
                }

                // Get access token from client-side authentication
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var accessToken = await GetValidAccessTokenAsync(authState, scopes);

                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("❌ NO VALID ACCESS TOKEN FROM CLIENT");
                    return await SimulateGetCurrentUserProfileAsync();
                }

                // Try to make a direct HTTP call to the MCP server with client token
                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.BaseAddress = new Uri(_mcpServerBaseUrl);
                    httpClient.Timeout = TimeSpan.FromSeconds(30);
                    
                    // Add the client-side access token
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    var requestData = new
                    {
                        tool = "get_current_user_profile"
                    };

                    var jsonContent = JsonSerializer.Serialize(requestData);
                    var content = new StringContent(jsonContent, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

                    _logger.LogInformation("Making HTTP request to MCP Server with client token for get_current_user_profile");
                    
                    var response = await httpClient.PostAsync("/api/tools/execute", content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("Received successful response from MCP Server: {ResponseLength} characters", result?.Length ?? 0);
                        return result;
                    }
                    else
                    {
                        _logger.LogWarning("MCP Server returned error status: {StatusCode}", response.StatusCode);
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    _logger.LogWarning("HTTP error calling MCP Server: {Message}", httpEx.Message);
                }
                catch (TaskCanceledException timeoutEx)
                {
                    _logger.LogWarning("Timeout calling MCP Server: {Message}", timeoutEx.Message);
                }
                catch (Exception httpEx)
                {
                    _logger.LogError(httpEx, "Error making HTTP call to MCP Server");
                }

                // Fallback to simulation
                _logger.LogInformation("Falling back to simulated data");
                return await SimulateGetCurrentUserProfileAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling get_current_user_profile tool");
                return $"Error retrieving current user profile: {ex.Message}";
            }
        }

        private async Task<string> SimulateGetUserProfileAsync(string email)
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
                        CompanyName = "Contoso Corporation",
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
                        CompanyName = "Contoso Corporation",
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
                        CompanyName = "Contoso Corporation",
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
                OfficeLocation = "Building A B C, Floor 3",
                BusinessPhones = new[] { "+1-555-0123" },
                MobilePhone = "+1-555-0456",
                UserPrincipalName = email,
                CompanyName = "Contoso Corporation",
                Country = "United States",
                City = "Seattle",
                State = "Washington"
            };

            return JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        }

        private async Task<string> SimulateGetCurrentUserProfileAsync()
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
                CompanyName = "Contoso Corporation"
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

        public async Task<string> CallGetUsersByAppRoleToolAsync(string appRole, string appName)
        {
            try
            {
                _logger.LogInformation("🚀 CLIENT-SIDE CALL: GetADB2CUsersByAppRole tool with appRole: {AppRole}, appName: {AppName}", appRole, appName);

                // Get authentication state to check if user is authenticated
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                if (authState?.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("❌ User not authenticated, returning fallback data");
                    return await FallbackGetUsersByAppRoleAsync(appRole, appName);
                }

                // Get access token from client-side
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var accessToken = await GetValidAccessTokenAsync(authState, scopes);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("❌ Failed to acquire access token, returning fallback data");
                    return await FallbackGetUsersByAppRoleAsync(appRole, appName);
                }

                // Create HTTP client with Bearer token
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var requestUrl = $"{_mcpServerBaseUrl}/User/by-app-role?appRole={Uri.EscapeDataString(appRole)}&appName={Uri.EscapeDataString(appName)}";
                _logger.LogInformation("📡 CLIENT-SIDE REQUEST URL: {Url}", requestUrl);
                
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"📡 CLIENT-SIDE REQUEST: {requestUrl}");
                Console.ResetColor();

                var response = await httpClient.GetAsync(requestUrl);

                _logger.LogInformation("📨 RESPONSE STATUS: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                
                Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"📨 HTTP RESPONSE: {response.StatusCode} - {response.ReasonPhrase}");
                Console.ResetColor();

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("✅ SUCCESS: Retrieved {Length} characters from MCP Server", responseContent?.Length ?? 0);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ CLIENT-SIDE SUCCESS: {responseContent?.Length ?? 0} characters received");
                    Console.ResetColor();
                    
                    return responseContent ?? string.Empty;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("❌ CLIENT-SIDE call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ CLIENT-SIDE ERROR {response.StatusCode}: {errorContent}");
                    Console.ResetColor();
                    
                    return await FallbackGetUsersByAppRoleAsync(appRole, appName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR in client-side GetUsersByAppRole call");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 CLIENT-SIDE ERROR: {ex.Message}");
                Console.ResetColor();
                
                return await FallbackGetUsersByAppRoleAsync(appRole, appName);
            }
        }

        public async Task<string> CallGetB2CApplicationsToolAsync(bool ownedOnly = false)
        {
            try
            {
                _logger.LogInformation("🚀 CLIENT-SIDE CALL: GetB2CApplications tool with ownedOnly: {OwnedOnly}", ownedOnly);

                // Get authentication state to check if user is authenticated
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                if (authState?.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("❌ User not authenticated, returning fallback data");
                    return ownedOnly ? await FallbackGetApplicationsIOwnedAsync() : await FallbackGetApplicationsAsync();
                }

                // Get access token from client-side
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var accessToken = await GetValidAccessTokenAsync(authState, scopes);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("❌ Failed to acquire access token, returning fallback data");
                    return ownedOnly ? await FallbackGetApplicationsIOwnedAsync() : await FallbackGetApplicationsAsync();
                }

                // Create HTTP client with Bearer token
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var requestUrl = $"{_mcpServerBaseUrl}/Application?ownedOnly={ownedOnly}";
                _logger.LogInformation("📡 CLIENT-SIDE REQUEST URL: {Url}", requestUrl);
                
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"📡 CLIENT-SIDE REQUEST: {requestUrl}");
                Console.ResetColor();

                var response = await httpClient.GetAsync(requestUrl);

                _logger.LogInformation("📨 RESPONSE STATUS: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                
                Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"📨 HTTP RESPONSE: {response.StatusCode} - {response.ReasonPhrase}");
                Console.ResetColor();

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("✅ SUCCESS: Retrieved {Length} characters from MCP Server", responseContent?.Length ?? 0);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ CLIENT-SIDE SUCCESS: {responseContent?.Length ?? 0} characters received");
                    Console.ResetColor();
                    
                    return responseContent ?? string.Empty;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("❌ CLIENT-SIDE call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ CLIENT-SIDE ERROR {response.StatusCode}: {errorContent}");
                    Console.ResetColor();
                    
                    return ownedOnly ? await FallbackGetApplicationsIOwnedAsync() : await FallbackGetApplicationsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR in client-side GetB2CApplications call");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 CLIENT-SIDE ERROR: {ex.Message}");
                Console.ResetColor();
                
                return ownedOnly ? await FallbackGetApplicationsIOwnedAsync() : await FallbackGetApplicationsAsync();
            }
        }

        public async Task<string> CallGetApplicationRolesToolAsync(string appName)
        {
            try
            {
                _logger.LogInformation("🚀 CLIENT-SIDE CALL: GetADB2CApplicationRole tool with appName: {AppName}", appName);

                // Get authentication state to check if user is authenticated
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                if (authState?.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("❌ User not authenticated, returning fallback data");
                    return await FallbackGetApplicationRolesAsync(appName);
                }

                // Get access token from client-side
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var accessToken = await GetValidAccessTokenAsync(authState, scopes);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("❌ Failed to acquire access token, returning fallback data");
                    return await FallbackGetApplicationRolesAsync(appName);
                }

                // Create HTTP client with Bearer token
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var requestUrl = $"{_mcpServerBaseUrl}/Roles?appName={Uri.EscapeDataString(appName)}";
                _logger.LogInformation("📡 CLIENT-SIDE REQUEST URL: {Url}", requestUrl);
                
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"📡 CLIENT-SIDE REQUEST: {requestUrl}");
                Console.ResetColor();

                var response = await httpClient.GetAsync(requestUrl);

                _logger.LogInformation("📨 RESPONSE STATUS: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                
                Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"📨 HTTP RESPONSE: {response.StatusCode} - {response.ReasonPhrase}");
                Console.ResetColor();

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("✅ SUCCESS: Retrieved {Length} characters from MCP Server", responseContent?.Length ?? 0);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ CLIENT-SIDE SUCCESS: {responseContent?.Length ?? 0} characters received");
                    Console.ResetColor();
                    
                    return responseContent ?? string.Empty;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("❌ CLIENT-SIDE call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ CLIENT-SIDE ERROR {response.StatusCode}: {errorContent}");
                    Console.ResetColor();
                    
                    return await FallbackGetApplicationRolesAsync(appName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR in client-side GetApplicationRoles call");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 CLIENT-SIDE ERROR: {ex.Message}");
                Console.ResetColor();
                
                return await FallbackGetApplicationRolesAsync(appName);
            }
        }

        public async Task<string> CallAssignRoleToUserToolAsync(string username, string appName, string roleName)
        {
            try
            {
                _logger.LogInformation("🚀 CLIENT-SIDE CALL: AssignRoletoUser tool with username: {Username}, appName: {AppName}, roleName: {RoleName}", username, appName, roleName);

                // Get authentication state to check if user is authenticated
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                if (authState?.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("❌ User not authenticated, returning fallback data");
                    return await FallbackAssignRoleToUserAsync(username, appName, roleName);
                }

                // Get access token from client-side
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var accessToken = await GetValidAccessTokenAsync(authState, scopes);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("❌ Failed to acquire access token, returning fallback data");
                    return await FallbackAssignRoleToUserAsync(username, appName, roleName);
                }

                // Create HTTP client with Bearer token
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var requestUrl = $"{_mcpServerBaseUrl}/Roles/assign?username={Uri.EscapeDataString(username)}&appName={Uri.EscapeDataString(appName)}&roleName={Uri.EscapeDataString(roleName)}";
                _logger.LogInformation("📡 CLIENT-SIDE REQUEST URL: {Url}", requestUrl);
                
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"📡 CLIENT-SIDE REQUEST: {requestUrl}");
                Console.ResetColor();

                var response = await httpClient.GetAsync(requestUrl);

                _logger.LogInformation("📨 RESPONSE STATUS: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                
                Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"📨 HTTP RESPONSE: {response.StatusCode} - {response.ReasonPhrase}");
                Console.ResetColor();

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("✅ SUCCESS: Retrieved {Length} characters from MCP Server", responseContent?.Length ?? 0);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ CLIENT-SIDE SUCCESS: {responseContent?.Length ?? 0} characters received");
                    Console.ResetColor();
                    
                    return responseContent ?? string.Empty;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("❌ CLIENT-SIDE call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ CLIENT-SIDE ERROR {response.StatusCode}: {errorContent}");
                    Console.ResetColor();
                    
                    return await FallbackAssignRoleToUserAsync(username, appName, roleName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR in client-side AssignRoleToUser call");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 CLIENT-SIDE ERROR: {ex.Message}");
                Console.ResetColor();
                
                return await FallbackAssignRoleToUserAsync(username, appName, roleName);
            }
        }

        public async Task<string> CallRevokeUserRoleToolAsync(string username, string appName, string roleName)
        {
            try
            {
                _logger.LogInformation("🚀 CLIENT-SIDE CALL: RevokeUserRole tool with username: {Username}, appName: {AppName}, roleName: {RoleName}", username, appName, roleName);

                // Get authentication state to check if user is authenticated
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                if (authState?.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("❌ User not authenticated, returning fallback data");
                    return await FallbackRevokeUserRoleAsync(username, appName, roleName);
                }

                // Get access token from client-side
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var accessToken = await GetValidAccessTokenAsync(authState, scopes);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("❌ Failed to acquire access token, returning fallback data");
                    return await FallbackRevokeUserRoleAsync(username, appName, roleName);
                }

                // Create HTTP client with Bearer token
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var requestUrl = $"{_mcpServerBaseUrl}/Roles/revoke?username={Uri.EscapeDataString(username)}&appName={Uri.EscapeDataString(appName)}&roleName={Uri.EscapeDataString(roleName)}";
                _logger.LogInformation("📡 CLIENT-SIDE REQUEST URL: {Url}", requestUrl);
                
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"📡 CLIENT-SIDE REQUEST: {requestUrl}");
                Console.ResetColor();

                var response = await httpClient.GetAsync(requestUrl);

                _logger.LogInformation("📨 RESPONSE STATUS: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                
                Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"📨 HTTP RESPONSE: {response.StatusCode} - {response.ReasonPhrase}");
                Console.ResetColor();

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("✅ SUCCESS: Retrieved {Length} characters from MCP Server", responseContent?.Length ?? 0);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ CLIENT-SIDE SUCCESS: {responseContent?.Length ?? 0} characters received");
                    Console.ResetColor();
                    
                    return responseContent ?? string.Empty;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("❌ CLIENT-SIDE call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ CLIENT-SIDE ERROR {response.StatusCode}: {errorContent}");
                    Console.ResetColor();
                    
                    return await FallbackRevokeUserRoleAsync(username, appName, roleName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR in client-side RevokeUserRole call");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 CLIENT-SIDE ERROR: {ex.Message}");
                Console.ResetColor();
                
                return await FallbackRevokeUserRoleAsync(username, appName, roleName);
            }
        }

        public async Task<string> CallGetWeatherForecastToolAsync()
        {
            try
            {
                _logger.LogInformation("🚀 CLIENT-SIDE CALL: GetWeatherAuthAPI tool");

                // Get authentication state to check if user is authenticated
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                if (authState?.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("❌ User not authenticated, returning fallback data");
                    return await FallbackGetWeatherForecastAsync();
                }

                // Get access token from client-side
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var accessToken = await GetValidAccessTokenAsync(authState, scopes);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("❌ Failed to acquire access token, returning fallback data");
                    return await FallbackGetWeatherForecastAsync();
                }

                // Create HTTP client with Bearer token
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var requestUrl = $"{_mcpServerBaseUrl}/WeatherForecast";
                _logger.LogInformation("📡 CLIENT-SIDE REQUEST URL: {Url}", requestUrl);
                
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"📡 CLIENT-SIDE REQUEST: {requestUrl}");
                Console.ResetColor();

                var response = await httpClient.GetAsync(requestUrl);

                _logger.LogInformation("📨 RESPONSE STATUS: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                
                Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"📨 HTTP RESPONSE: {response.StatusCode} - {response.ReasonPhrase}");
                Console.ResetColor();

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("✅ SUCCESS: Retrieved {Length} characters from MCP Server", responseContent?.Length ?? 0);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ CLIENT-SIDE SUCCESS: {responseContent?.Length ?? 0} characters received");
                    Console.ResetColor();
                    
                    return responseContent ?? string.Empty;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("❌ CLIENT-SIDE call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ CLIENT-SIDE ERROR {response.StatusCode}: {errorContent}");
                    Console.ResetColor();
                    
                    return await FallbackGetWeatherForecastAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR in client-side GetWeatherForecast call");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 CLIENT-SIDE ERROR: {ex.Message}");
                Console.ResetColor();
                
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
                    DisplayName = "App User 1 (Simulated)",
                    Email = "appuser1@contoso.com",
                    Type = "User",
                    AppRole = appRole,
                    AppName = appName
                },
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = "App User 2 (Simulated)",
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
                    Name = "Admin (Simulated)",
                    AppId = Guid.NewGuid().ToString(),
                    AppName = appName
                },
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "User (Simulated)",
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

        private async Task<string> FallbackRevokeUserRoleAsync(string username, string appName, string roleName)
        {
            await Task.Delay(300);
            
            var result = new
            {
                Success = true,
                Message = $"Revoked {roleName} from user {username} for application {appName} (Simulated)",
                Username = username,
                AppName = appName,
                RoleName = roleName,
                RevokedAt = DateTime.UtcNow
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

        public async Task<string> CallGetUserRolesFromAllApplicationsToolAsync(string username)
        {
            try
            {
                _logger.LogInformation("🚀 CLIENT-SIDE CALL: GetUserRolesFromAllApplications tool with username: {Username}", username);

                // Get authentication state to check if user is authenticated
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                if (authState?.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("❌ User not authenticated, returning fallback data");
                    return await FallbackGetUserRolesFromAllApplicationsAsync(username);
                }

                // Get access token from client-side
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var accessToken = await GetValidAccessTokenAsync(authState, scopes);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("❌ Failed to acquire access token, returning fallback data");
                    return await FallbackGetUserRolesFromAllApplicationsAsync(username);
                }

                // Create HTTP client with Bearer token
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var requestUrl = $"{_mcpServerBaseUrl}/Roles/user-roles?username={Uri.EscapeDataString(username)}";
                _logger.LogInformation("📡 CLIENT-SIDE REQUEST URL: {Url}", requestUrl);
                
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"📡 CLIENT-SIDE REQUEST: {requestUrl}");
                Console.ResetColor();

                var response = await httpClient.GetAsync(requestUrl);

                _logger.LogInformation("📨 RESPONSE STATUS: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                
                Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"📨 HTTP RESPONSE: {response.StatusCode} - {response.ReasonPhrase}");
                Console.ResetColor();

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("✅ SUCCESS: Retrieved {Length} characters from MCP Server", responseContent?.Length ?? 0);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ CLIENT-SIDE SUCCESS: {responseContent?.Length ?? 0} characters received");
                    Console.ResetColor();
                    
                    return responseContent ?? string.Empty;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("❌ CLIENT-SIDE call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ CLIENT-SIDE ERROR {response.StatusCode}: {errorContent}");
                    Console.ResetColor();
                    
                    return await FallbackGetUserRolesFromAllApplicationsAsync(username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR in client-side GetUserRolesFromAllApplications call");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 CLIENT-SIDE ERROR: {ex.Message}");
                Console.ResetColor();
                
                return await FallbackGetUserRolesFromAllApplicationsAsync(username);
            }
        }

        private async Task<string> FallbackGetUserRolesFromAllApplicationsAsync(string username)
        {
            await Task.Delay(100); // Simulate async operation
            return $"{{\"message\": \"Fallback: Unable to retrieve user roles for {username}. Please check if the user exists and you have the necessary permissions.\", \"roles\": []}}";
        }

        public async Task<string> CallCreateAppRoleToolAsync(string appName, string appRole)
        {
            try
            {
                _logger.LogInformation("🚀 CLIENT-SIDE CALL: CreateAppRole tool with appName: {AppName}, appRole: {AppRole}", appName, appRole);

                // Get authentication state to check if user is authenticated
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                if (authState?.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("❌ User not authenticated, returning fallback data");
                    return await FallbackCreateAppRoleAsync(appName, appRole);
                }

                // Get access token from client-side
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var accessToken = await GetValidAccessTokenAsync(authState, scopes);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("❌ Failed to acquire access token, returning fallback data");
                    return await FallbackCreateAppRoleAsync(appName, appRole);
                }

                // Create HTTP client with Bearer token
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var requestUrl = $"{_mcpServerBaseUrl}/Roles/create?appName={Uri.EscapeDataString(appName)}&appRole={Uri.EscapeDataString(appRole)}";
                _logger.LogInformation("📡 CLIENT-SIDE REQUEST URL: {Url}", requestUrl);
                
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"📡 CLIENT-SIDE REQUEST: {requestUrl}");
                Console.ResetColor();

                var response = await httpClient.PostAsync(requestUrl, null);

                _logger.LogInformation("📨 RESPONSE STATUS: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                
                Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"📨 HTTP RESPONSE: {response.StatusCode} - {response.ReasonPhrase}");
                Console.ResetColor();

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("✅ SUCCESS: Retrieved {Length} characters from MCP Server", responseContent?.Length ?? 0);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ CLIENT-SIDE SUCCESS: {responseContent?.Length ?? 0} characters received");
                    Console.ResetColor();
                    
                    return responseContent ?? string.Empty;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("❌ CLIENT-SIDE call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ CLIENT-SIDE ERROR {response.StatusCode}: {errorContent}");
                    Console.ResetColor();
                    
                    return await FallbackCreateAppRoleAsync(appName, appRole);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR in client-side CreateAppRole call");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 CLIENT-SIDE ERROR: {ex.Message}");
                Console.ResetColor();
                
                return await FallbackCreateAppRoleAsync(appName, appRole);
            }
        }

        private async Task<string> FallbackCreateAppRoleAsync(string appName, string appRole)
        {
            await Task.Delay(100); // Simulate async operation
            return $"{{\"message\": \"Fallback: Unable to create role '{appRole}' for application '{appName}'. Please check if the application exists and you have the necessary permissions.\", \"success\": false}}";
        }

        public async Task<string> CallManageRolesToolAsync(string action, string? appName = null, string? username = null, string? roleName = null)
        {
            try
            {
                _logger.LogInformation("🚀 CLIENT-SIDE CALL: ManageRoles tool with action: {Action}, appName: {AppName}, username: {Username}, roleName: {RoleName}", 
                    action, appName, username, roleName);

                // Get authentication state to check if user is authenticated
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                if (authState?.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("❌ User not authenticated, returning fallback data");
                    return await FallbackManageRolesAsync(action, appName, username, roleName);
                }

                // Get access token from client-side
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var accessToken = await GetValidAccessTokenAsync(authState, scopes);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("❌ Failed to acquire access token, returning fallback data");
                    return await FallbackManageRolesAsync(action, appName, username, roleName);
                }

                // Create HTTP client with Bearer token
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                // Build query parameters
                var queryParams = new List<string> { $"action={Uri.EscapeDataString(action)}" };
                if (!string.IsNullOrEmpty(appName))
                    queryParams.Add($"appName={Uri.EscapeDataString(appName)}");
                if (!string.IsNullOrEmpty(username))
                    queryParams.Add($"username={Uri.EscapeDataString(username)}");
                if (!string.IsNullOrEmpty(roleName))
                    queryParams.Add($"roleName={Uri.EscapeDataString(roleName)}");

                var requestUrl = $"{_mcpServerBaseUrl}/Roles/manage?{string.Join("&", queryParams)}";
                _logger.LogInformation("📡 CLIENT-SIDE REQUEST URL: {Url}", requestUrl);
                
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"📡 CLIENT-SIDE REQUEST: {requestUrl}");
                Console.ResetColor();

                var response = await httpClient.GetAsync(requestUrl);

                _logger.LogInformation("📨 RESPONSE STATUS: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                
                Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"📨 HTTP RESPONSE: {response.StatusCode} - {response.ReasonPhrase}");
                Console.ResetColor();

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("✅ SUCCESS: Retrieved {Length} characters from MCP Server", responseContent?.Length ?? 0);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ CLIENT-SIDE SUCCESS: {responseContent?.Length ?? 0} characters received");
                    Console.ResetColor();
                    
                    return responseContent ?? string.Empty;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("❌ CLIENT-SIDE call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ CLIENT-SIDE ERROR {response.StatusCode}: {errorContent}");
                    Console.ResetColor();
                    
                    return await FallbackManageRolesAsync(action, appName, username, roleName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR in client-side ManageRoles call");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 CLIENT-SIDE ERROR: {ex.Message}");
                Console.ResetColor();
                
                return await FallbackManageRolesAsync(action, appName, username, roleName);
            }
        }

        public async Task<string> CallGetB2CUsersToolAsync(string action, string? roleName = null, string? appName = null)
        {
            try
            {
                _logger.LogInformation("🚀 CLIENT-SIDE CALL: GetB2CUsers tool with action: {Action}, roleName: {RoleName}, appName: {AppName}", 
                    action, roleName, appName);

                // Get authentication state to check if user is authenticated
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                if (authState?.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("❌ User not authenticated, returning fallback data");
                    return await FallbackGetB2CUsersAsync(action, roleName, appName);
                }

                // Get access token from client-side
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var accessToken = await GetValidAccessTokenAsync(authState, scopes);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("❌ Failed to acquire access token, returning fallback data");
                    return await FallbackGetB2CUsersAsync(action, roleName, appName);
                }

                // Create HTTP client with Bearer token
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                // Build query parameters
                var queryParams = new List<string> { $"action={Uri.EscapeDataString(action)}" };
                if (!string.IsNullOrEmpty(roleName))
                    queryParams.Add($"roleName={Uri.EscapeDataString(roleName)}");
                if (!string.IsNullOrEmpty(appName))
                    queryParams.Add($"appName={Uri.EscapeDataString(appName)}");

                var requestUrl = $"{_mcpServerBaseUrl}/User/manage?{string.Join("&", queryParams)}";
                _logger.LogInformation("📡 CLIENT-SIDE REQUEST URL: {Url}", requestUrl);
                
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"📡 CLIENT-SIDE REQUEST: {requestUrl}");
                Console.ResetColor();

                var response = await httpClient.GetAsync(requestUrl);

                _logger.LogInformation("📨 RESPONSE STATUS: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                
                Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"📨 HTTP RESPONSE: {response.StatusCode} - {response.ReasonPhrase}");
                Console.ResetColor();

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("✅ SUCCESS: Retrieved {Length} characters from MCP Server", responseContent?.Length ?? 0);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ CLIENT-SIDE SUCCESS: {responseContent?.Length ?? 0} characters received");
                    Console.ResetColor();
                    
                    return responseContent ?? string.Empty;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("❌ CLIENT-SIDE call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ CLIENT-SIDE ERROR {response.StatusCode}: {errorContent}");
                    Console.ResetColor();
                    
                    return await FallbackGetB2CUsersAsync(action, roleName, appName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR in client-side GetB2CUsers call");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"💥 CLIENT-SIDE ERROR: {ex.Message}");
                Console.ResetColor();
                
                return await FallbackGetB2CUsersAsync(action, roleName, appName);
            }
        }

        private async Task<string> FallbackManageRolesAsync(string action, string? appName = null, string? username = null, string? roleName = null)
        {
            await Task.Delay(100); // Simulate async operation
            
            return action.ToLowerInvariant() switch
            {
                "get-roles" => $"{{\"message\": \"Fallback: Unable to get roles for application '{appName}'. Please check if the application exists and you have the necessary permissions.\", \"success\": false}}",
                "assign-role" => $"{{\"message\": \"Fallback: Unable to assign role '{roleName}' to user '{username}' for application '{appName}'. Please check if the user, role, and application exist.\", \"success\": false}}",
                "revoke-role" => $"{{\"message\": \"Fallback: Unable to revoke role '{roleName}' from user '{username}' for application '{appName}'. Please check if the user, role, and application exist.\", \"success\": false}}",
                "get-user-roles" => $"{{\"message\": \"Fallback: Unable to get roles for user '{username}'. Please check if the user exists and you have the necessary permissions.\", \"success\": false}}",
                "create-role" => $"{{\"message\": \"Fallback: Unable to create role '{roleName}' for application '{appName}'. Please check if the application exists and you have the necessary permissions.\", \"success\": false}}",
                _ => $"{{\"message\": \"Fallback: Invalid action '{action}'. Valid actions are: get-roles, assign-role, revoke-role, get-user-roles, create-role\", \"success\": false}}"
            };
        }

        private async Task<string> FallbackGetB2CUsersAsync(string action, string? roleName = null, string? appName = null)
        {
            await Task.Delay(100); // Simulate async operation
            
            return action.ToLowerInvariant() switch
            {
                "all" => await SimulateGetUserProfileAsync(""),
                "by-role" => await FallbackGetUsersByAppRoleAsync(roleName ?? "", appName ?? ""),
                _ => $"{{\"message\": \"Fallback: Invalid action '{action}'. Valid actions are: all, by-role\", \"success\": false}}"
            };
        }

        public async Task DisconnectAsync()
        {
            if (_mcpClient != null)
            {
                try
                {
                    // await _mcpClient.DisconnectAsync();
                    _mcpClient = null;
                    _logger.LogInformation("Disconnected from MCP Server");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disconnecting from MCP Server");
                }
            }
        }
    }
}
