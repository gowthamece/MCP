using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace MCP.ADB2C.Middleware
{
    public class AuthorizationLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthorizationLoggingMiddleware> _logger;

        public AuthorizationLoggingMiddleware(RequestDelegate next, ILogger<AuthorizationLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Log all incoming requests with authorization details
            var path = context.Request.Path;
            var method = context.Request.Method;
            
            _logger.LogInformation("=== INCOMING REQUEST ===");
            _logger.LogInformation("Path: {Path}, Method: {Method}", path, method);
            
            // Log Authorization header
            if (context.Request.Headers.ContainsKey("Authorization"))
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    _logger.LogInformation("Authorization Header Present: {AuthHeaderStart}...", 
                        authHeader.Length > 50 ? authHeader.Substring(0, 50) : authHeader);
                    
                    if (authHeader.StartsWith("Bearer "))
                    {
                        var token = authHeader.Substring(7);
                        _logger.LogInformation("Bearer Token Length: {TokenLength}", token.Length);
                        _logger.LogInformation("Token Preview: {TokenStart}...", 
                            token.Length > 20 ? token.Substring(0, 20) : token);
                    }
                }
            }
            else
            {
                _logger.LogWarning("❌ NO AUTHORIZATION HEADER FOUND");
            }

            // Log other relevant headers
            foreach (var header in context.Request.Headers)
            {
                if (header.Key.StartsWith("X-") || header.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Header {Key}: {Value}", header.Key, header.Value);
                }
            }

            // Continue to next middleware
            await _next(context);

            // Log response status
            _logger.LogInformation("=== RESPONSE ===");
            _logger.LogInformation("Status Code: {StatusCode}", context.Response.StatusCode);
            
            if (context.Response.StatusCode == 401)
            {
                _logger.LogError("❌ UNAUTHORIZED RESPONSE - Authentication failed");
                
                // Log user claims if available
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    _logger.LogInformation("User is authenticated but access was denied");
                    _logger.LogInformation("User Identity Name: {UserName}", context.User.Identity.Name);
                    _logger.LogInformation("User Claims Count: {ClaimsCount}", context.User.Claims.Count());
                    
                    foreach (var claim in context.User.Claims.Take(10))
                    {
                        _logger.LogInformation("Claim {Type}: {Value}", claim.Type, claim.Value);
                    }
                }
                else
                {
                    _logger.LogWarning("User is NOT authenticated");
                }
            }
            else if (context.Response.StatusCode == 200)
            {
                _logger.LogInformation("✅ SUCCESS - Request completed successfully");
            }
            
            _logger.LogInformation("=== END REQUEST ===");
        }
    }
}
