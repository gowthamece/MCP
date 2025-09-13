using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace MCP.ADB2C.Client.Services
{
    public class MCPToolAnalysisResponse
    {
        [JsonPropertyName("shouldCallTool")]
        public bool ShouldCallTool { get; set; }
        
        [JsonPropertyName("toolName")]
        public string? ToolName { get; set; }
        
        [JsonPropertyName("parameters")]
        public Dictionary<string, string>? Parameters { get; set; }
        
        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }

    public class MCPClientService
    {
        private readonly AzureOpenAIClient _client;
        private readonly string _deploymentName;
        private readonly List<ChatMessage> _conversationHistory;
        private readonly HttpClient _httpClient;
        private readonly RealMCPClientService _realMCPClient;
        private readonly HttpMCPClientService _httpMCPClient;

        public MCPClientService(IConfiguration configuration, HttpClient httpClient, RealMCPClientService realMCPClient, HttpMCPClientService httpMCPClient)
        {
            var endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint");
            var key = configuration["AzureOpenAI:Key"] ?? throw new ArgumentNullException("AzureOpenAI:Key");
            _deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? throw new ArgumentNullException("AzureOpenAI:DeploymentName");
            _client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key));
            _httpClient = httpClient;
            _realMCPClient = realMCPClient;
            _httpMCPClient = httpMCPClient;
            
            // Initialize conversation with system message that includes all MCP tool awareness
            _conversationHistory = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are a helpful AI assistant with access to Azure AD B2C management tools through MCP. 
                
                Available MCP Tools:
                1. GetB2CUsers(action, roleName, appName) - Unified user management tool with actions:
                   - all: Gets all Azure AD B2C users (no additional parameters needed)
                   - by-role: Gets users by application role (requires roleName and appName)
                2. GetB2CApplications(ownedOnly) - Gets Azure AD B2C applications (ownedOnly: false=all apps, true=only owned apps)
                3. ManageRoles(action, appName, username, roleName) - Unified role management tool with actions:
                   - get-roles: Gets roles for a specific application (requires appName)
                   - assign-role: Assigns a role to a user (requires username, appName, roleName)
                   - revoke-role: Revokes a role from a user (requires username, appName, roleName)
                   - get-user-roles: Gets all roles for a user across applications (requires username)
                   - create-role: Creates a new role for an application (requires appName, roleName)
                4. GetWeatherAuthAPI() - Gets weather forecast (demo tool)
                
                When users ask questions, determine which tool(s) to use based on their request:
                - User management: Use GetB2CUsers with appropriate action:
                  * For all users: action=all
                  * For users by role: action=by-role, roleName=<role>, appName=<app>
                - Application management: GetB2CApplications (use ownedOnly=false for all applications, ownedOnly=true for owned applications)
                - Role management: Use ManageRoles with appropriate action:
                  * For getting application roles: action=get-roles, appName=<app name>
                  * For assigning roles: action=assign-role, username=<user>, appName=<app>, roleName=<role>
                  * For revoking roles: action=revoke-role, username=<user>, appName=<app>, roleName=<role>
                  * For user role overview: action=get-user-roles, username=<user>
                  * For creating new roles: action=create-role, appName=<app>, roleName=<role>
                - Weather: GetWeatherAuthAPI
                
                Use GetB2CUsers with action=all when users ask about:
                - All users in the system
                - List of all users
                - User directory
                - Complete user list
                
                Use GetB2CUsers with action=by-role when users ask about:
                - Users with a specific role
                - Users assigned to a particular application role
                - Role-based user queries
                
                Always format results as HTML tables or well-structured content for better readability.
                If you need parameters for a tool, ask the user to provide them.")
            };
        }

        public async Task<string> GetChatCompletionAsync(string userMessage)
        {
            try
            {
                var chatClient = _client.GetChatClient(_deploymentName);

                Console.WriteLine($"[MCPClientService] Processing message: {userMessage}");

                // Add user message to conversation history
                _conversationHistory.Add(new UserChatMessage(userMessage));

                // Check if the user is asking for MCP tool information and determine which tool to use
                var toolRequest = await AnalyzeMCPToolRequestAsync(userMessage);
                Console.WriteLine($"[MCPClientService] Tool request: {toolRequest.ShouldCallTool}, Tool: {toolRequest.ToolName}");

                if (toolRequest.ShouldCallTool)
                {
                    var mcpResult = await CallMCPToolAsync(toolRequest.ToolName, toolRequest.Parameters, userMessage);
                    if (!string.IsNullOrEmpty(mcpResult))
                    {
                        // Add the MCP result as context for the AI
                        var contextMessage = new SystemChatMessage($"MCP Tool Result ({toolRequest.ToolName}): {mcpResult}");
                        _conversationHistory.Add(contextMessage);

                        // Add instruction to format appropriately
                        _conversationHistory.Add(new SystemChatMessage("Please format the above data in a well-structured, readable format (HTML table, lists, or formatted text as appropriate)."));
                    }
                }

                var response = await chatClient.CompleteChatAsync(_conversationHistory);
                var assistantResponse = response.Value.Content[0].Text;

                // Add assistant response to conversation history
                _conversationHistory.Add(new AssistantChatMessage(assistantResponse));

                return assistantResponse;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[MCPClientService] Error: {ex.Message}");
                return $"I encountered an error while processing your request: {ex.Message}";
            }
        }

        private async Task<MCPToolRequest> AnalyzeMCPToolRequestAsync(string userMessage)
        {
            var message = userMessage.ToLowerInvariant();
            Console.WriteLine($"[MCPClientService] Analyzing message: '{message}'");
            
            // Use OpenAI to intelligently detect tool and extract parameters
            return await AnalyzeWithOpenAIAsync(userMessage);
        }

        private async Task<MCPToolRequest> AnalyzeWithOpenAIAsync(string userMessage)
        {
            try
            {
                var chatClient = _client.GetChatClient(_deploymentName);
                
                var systemPrompt = @"
You are an AI assistant that analyzes user requests to determine which MCP (Model Context Protocol) tool to use and extract the required parameters.

Available MCP Tools:
1. GetB2CUsers - Unified user management tool with action-based routing
   Parameters: action (required), roleName (optional), appName (optional)
   Actions:
   - all: Gets all Azure AD B2C users (no additional parameters)
   - by-role: Gets users by application role (requires roleName and appName)
2. GetB2CApplications - Gets Azure AD B2C applications with optional filtering
   Parameters: ownedOnly (boolean: false=all apps, true=only owned apps)
3. ManageRoles - Unified role management tool with multiple actions
   Parameters: action (required), appName (optional), username (optional), roleName (optional)
   Actions:
   - get-roles: Gets roles for a specific application (requires appName)
   - assign-role: Assigns a role to a user (requires username, appName, roleName)
   - revoke-role: Revokes a role from a user (requires username, appName, roleName)
   - get-user-roles: Gets all roles for a user across applications (requires username)
   - create-role: Creates a new role for an application (requires appName, roleName)
4. GetWeatherAuthAPI - Gets weather forecast (no parameters required)

Analyze the user message and respond with a JSON object in this exact format:
{
  ""shouldCallTool"": true/false,
  ""toolName"": ""ToolName"" or null,
  ""parameters"": {""key"": ""value""} or {},
  ""confidence"": 0.0-1.0
}

Rules:
- Only return shouldCallTool: true if you're confident (>0.7) about the tool selection
- Extract parameter values exactly as they appear in the user message (preserve case)
- If asking for users with a specific role, use GetB2CUsers with action=by-role
- If asking for all users, use GetB2CUsers with action=all
- If asking for roles of an application, use ManageRoles with action=get-roles
- If asking for applications, use GetB2CApplications (ownedOnly=false for all, ownedOnly=true for owned)
- If assigning roles, use ManageRoles with action=assign-role
- If revoking/removing roles, use ManageRoles with action=revoke-role
- If asking for all roles for a specific user, use ManageRoles with action=get-user-roles
- If creating new roles, use ManageRoles with action=create-role
- For weather requests, use GetWeatherAuthAPI

Examples:
User: ""Get all users""
Response: {""shouldCallTool"": true, ""toolName"": ""GetB2CUsers"", ""parameters"": {""action"": ""all""}, ""confidence"": 0.95}

User: ""Get users with Manager role in MyApp""
Response: {""shouldCallTool"": true, ""toolName"": ""GetB2CUsers"", ""parameters"": {""action"": ""by-role"", ""roleName"": ""Manager"", ""appName"": ""MyApp""}, ""confidence"": 0.9}

User: ""Get roles for MyApp application""
Response: {""shouldCallTool"": true, ""toolName"": ""ManageRoles"", ""parameters"": {""action"": ""get-roles"", ""appName"": ""MyApp""}, ""confidence"": 0.95}

User: ""Assign admin role to john@example.com for MyApp""
Response: {""shouldCallTool"": true, ""toolName"": ""ManageRoles"", ""parameters"": {""action"": ""assign-role"", ""username"": ""john@example.com"", ""appName"": ""MyApp"", ""roleName"": ""admin""}, ""confidence"": 0.95}

User: ""Get users assigned to Manager role for MyApp application""
Response: {""shouldCallTool"": true, ""toolName"": ""GetADB2CUsersByAppRole"", ""parameters"": {""appRole"": ""Manager"", ""appName"": ""MyApp""}, ""confidence"": 0.9}
";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage($"Analyze this request: {userMessage}")
                };

                var response = await chatClient.CompleteChatAsync(messages);
                var aiResponse = response.Value.Content[0].Text;
                
                Console.WriteLine($"[MCPClientService] OpenAI tool analysis response: {aiResponse}");

                // Parse the JSON response
                var toolRequest = System.Text.Json.JsonSerializer.Deserialize<MCPToolAnalysisResponse>(aiResponse);
                
                if (toolRequest != null && toolRequest.ShouldCallTool && toolRequest.Confidence > 0.7)
                {
                    Console.WriteLine($"[MCPClientService] OpenAI selected tool: {toolRequest.ToolName} with confidence: {toolRequest.Confidence}");
                    return new MCPToolRequest 
                    { 
                        ShouldCallTool = true, 
                        ToolName = toolRequest.ToolName ?? "", 
                        Parameters = toolRequest.Parameters ?? new Dictionary<string, string>() 
                    };
                }
                else
                {
                    Console.WriteLine($"[MCPClientService] OpenAI confidence too low ({toolRequest?.Confidence}) or no tool selected");
                    return new MCPToolRequest { ShouldCallTool = false, ToolName = "", Parameters = new Dictionary<string, string>() };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MCPClientService] Error in OpenAI tool analysis: {ex.Message}");
                // Fallback to rule-based analysis
                return await FallbackRuleBasedAnalysisAsync(userMessage);
            }
        }

        private async Task<MCPToolRequest> FallbackRuleBasedAnalysisAsync(string userMessage)
        {
            var message = userMessage.ToLowerInvariant();
            Console.WriteLine($"[MCPClientService] Using fallback rule-based analysis for: '{message}'");
            
            // User management keywords
            if (message.Contains("all users") || message.Contains("list users") || message.Contains("get users") || 
                message.Contains("show users") || message.Contains("user details") || message.Contains("user profile") ||
                message.Contains("azure ad users") || message.Contains("b2c users"))
            {
                return new MCPToolRequest { ShouldCallTool = true, ToolName = "GetB2CUsers", Parameters = new Dictionary<string, string> { {"action", "all"} } };
            }

            // Users by app role - Enhanced patterns
            if (message.Contains("users by role") || message.Contains("users in role") || message.Contains("role users") ||
                message.Contains("app role users") || message.Contains("application role users") ||
                message.Contains("users assigned to") || message.Contains("users with role"))
            {
                var parameters = ExtractAppRoleParameters(message);
                parameters["action"] = "by-role";
                // Map old parameter names to new ones
                if (parameters.ContainsKey("appRole"))
                {
                    parameters["roleName"] = parameters["appRole"];
                    parameters.Remove("appRole");
                }
                return new MCPToolRequest { ShouldCallTool = true, ToolName = "GetB2CUsers", Parameters = parameters };
            }

            // Application management
            if (message.Contains("all applications") || message.Contains("list applications") || message.Contains("get applications") ||
                message.Contains("show applications") || message.Contains("app list"))
            {
                return new MCPToolRequest { ShouldCallTool = true, ToolName = "GetB2CApplications", Parameters = new Dictionary<string, string> { {"ownedOnly", "false"} } };
            }

            // Applications I own
            if (message.Contains("my applications") || message.Contains("applications i own") || message.Contains("owned applications") ||
                message.Contains("apps i own") || message.Contains("my apps"))
            {
                return new MCPToolRequest { ShouldCallTool = true, ToolName = "GetB2CApplications", Parameters = new Dictionary<string, string> { {"ownedOnly", "true"} } };
            }

            // Generic applications request - default to all
            if (message.Contains("applications"))
            {
                return new MCPToolRequest { ShouldCallTool = true, ToolName = "GetB2CApplications", Parameters = new Dictionary<string, string> { {"ownedOnly", "false"} } };
            }

            // Application roles
            if ((message.Contains("application roles") || message.Contains("app roles") || message.Contains("roles for") ||
                message.Contains("get roles") || message.Contains("show roles") || message.Contains("list roles") ||
                message.Contains("roles associate") || message.Contains("roles associated") || 
                (message.Contains("roles") && message.Contains("application")) ||
                (message.Contains("roles") && message.Contains("app"))) &&
                !message.Contains("users"))  // Exclude if it's asking for users by role
            {
                var parameters = ExtractAppNameParameters(message);
                Console.WriteLine($"[MCPClientService] Application roles detected. Extracted parameters: {string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"))}");
                return new MCPToolRequest { ShouldCallTool = true, ToolName = "GetADB2CApplicationRole", Parameters = parameters };
            }

            // Role assignment
            if (message.Contains("assign role") || message.Contains("assign user") || message.Contains("give role") ||
                message.Contains("add role") || message.Contains("role assignment"))
            {
                return new MCPToolRequest { ShouldCallTool = true, ToolName = "AssignRoletoUser", Parameters = ExtractRoleAssignmentParameters(message) };
            }

            // Role revocation
            if (message.Contains("revoke role") || message.Contains("remove role") || message.Contains("unassign role") ||
                message.Contains("revoke user") || message.Contains("remove user") || message.Contains("take away role") ||
                message.Contains("role revocation") || message.Contains("role removal"))
            {
                return new MCPToolRequest { ShouldCallTool = true, ToolName = "RevokeUserRole", Parameters = ExtractRoleAssignmentParameters(message) };
            }

            // Weather
            if (message.Contains("weather") || message.Contains("forecast") || message.Contains("temperature"))
            {
                return new MCPToolRequest { ShouldCallTool = true, ToolName = "GetWeatherAuthAPI", Parameters = new Dictionary<string, string>() };
            }

            return new MCPToolRequest { ShouldCallTool = false, ToolName = "", Parameters = new Dictionary<string, string>() };
        }

        private Dictionary<string, string> ExtractAppRoleParameters(string message)
        {
            var parameters = new Dictionary<string, string>();
            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            Console.WriteLine($"[ExtractAppRoleParameters] Analyzing: '{message}'");
            Console.WriteLine($"[ExtractAppRoleParameters] Words: [{string.Join(", ", words)}]");
            
            // Enhanced patterns for role and app extraction
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i].ToLower();
                
                // Pattern: "assigned to [ROLE] role" or "to [ROLE] role"
                if ((word == "assigned" || word == "to") && i + 2 < words.Length && words[i + 2].ToLower() == "role")
                {
                    parameters["appRole"] = words[i + 1];
                    Console.WriteLine($"[ExtractAppRoleParameters] Found role via 'assigned/to [role] role': {words[i + 1]}");
                    continue;
                }
                
                // Pattern: "role [ROLE]" (but not if previous word was "to" as that's handled above)
                if (word == "role" && i + 1 < words.Length && i > 0 && words[i - 1].ToLower() != "to")
                {
                    parameters["appRole"] = words[i + 1];
                    Console.WriteLine($"[ExtractAppRoleParameters] Found role via 'role [name]': {words[i + 1]}");
                    continue;
                }
                
                // Pattern: "application [APP]" or "app [APP]"
                if ((word == "application" || word == "app") && i + 1 < words.Length)
                {
                    var appName = words[i + 1];
                    // Handle hyphenated app names or names that end with comma/period
                    parameters["appName"] = appName.TrimEnd(',', '.', '!', '?');
                    Console.WriteLine($"[ExtractAppRoleParameters] Found app via 'application/app [name]': {appName}");
                    continue;
                }
                
                // Pattern: "for the application [APP]"
                if (word == "for" && i + 2 < words.Length && words[i + 1].ToLower() == "the" && words[i + 2].ToLower() == "application" && i + 3 < words.Length)
                {
                    parameters["appName"] = words[i + 3].TrimEnd(',', '.', '!', '?');
                    Console.WriteLine($"[ExtractAppRoleParameters] Found app via 'for the application [name]': {words[i + 3]}");
                    continue;
                }
            }
            
            // If we couldn't find appRole but found a hyphenated word that might be a role name, use it
            if (!parameters.ContainsKey("appRole"))
            {
                foreach (var word in words)
                {
                    if (word.Contains("-") && !IsCommonWord(word) && word != parameters.GetValueOrDefault("appName"))
                    {
                        // This might be a role name
                        parameters["appRole"] = word;
                        Console.WriteLine($"[ExtractAppRoleParameters] Using hyphenated word as potential role: {word}");
                        break;
                    }
                }
            }
            
            Console.WriteLine($"[ExtractAppRoleParameters] Final parameters: {string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"))}");
            return parameters;
        }

        private Dictionary<string, string> ExtractAppNameParameters(string message)
        {
            var parameters = new Dictionary<string, string>();
            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            Console.WriteLine($"[ExtractAppNameParameters] Message: '{message}'");
            Console.WriteLine($"[ExtractAppNameParameters] Words: [{string.Join(", ", words)}]");
            
            // Look for app name after keywords
            for (int i = 0; i < words.Length - 1; i++)
            {
                if ((words[i].ToLower() == "for" || words[i].ToLower() == "in" || words[i].ToLower() == "of" || 
                     words[i].ToLower() == "with" || words[i].ToLower() == "to" || words[i].ToLower() == "assigned" ||
                     words[i].ToLower() == "associate" || words[i].ToLower() == "associated") && 
                    i + 1 < words.Length)
                {
                    var nextWord = words[i + 1];
                    Console.WriteLine($"[ExtractAppNameParameters] Found keyword '{words[i]}' at index {i}, next word: '{nextWord}'");
                    
                    // If next word is "application", skip it and get the one before
                    if (nextWord.ToLower() == "application" && i >= 1)
                    {
                        Console.WriteLine($"[ExtractAppNameParameters] Next word is 'application', looking backwards from index {i}");
                        // Look for the app name before this pattern
                        for (int j = i - 1; j >= 0; j--)
                        {
                            if (!IsCommonWord(words[j]))
                            {
                                Console.WriteLine($"[ExtractAppNameParameters] Found app name before 'application': '{words[j]}'");
                                parameters["appName"] = words[j];
                                return parameters;
                            }
                        }
                    }
                    else
                    {
                        // Standard case: app name comes after the keyword
                        var appName = nextWord;
                        
                        // If the next word after appName is "application", we have the right app name
                        if (i + 2 < words.Length && words[i + 2].ToLower() == "application")
                        {
                            Console.WriteLine($"[ExtractAppNameParameters] Perfect match: '{words[i]}' -> '{appName}' -> 'application'");
                            parameters["appName"] = appName;
                            return parameters;
                        }
                        // If this word looks like an app name (not a common word), use it
                        else if (!IsCommonWord(appName))
                        {
                            Console.WriteLine($"[ExtractAppNameParameters] Using non-common word as app name: '{appName}'");
                            parameters["appName"] = appName;
                            return parameters;
                        }
                    }
                    break;
                }
            }
            
            // Fallback: look for words that end with "application" and get the word before it
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].ToLower() == "application" && i > 0)
                {
                    var potentialAppName = words[i - 1];
                    if (!IsCommonWord(potentialAppName))
                    {
                        parameters["appName"] = potentialAppName;
                        return parameters;
                    }
                }
            }
            
            // Final fallback: look for hyphenated names that might be app names
            if (!parameters.ContainsKey("appName"))
            {
                foreach (var word in words)
                {
                    // Look for hyphenated names that might be app names
                    if (word.Contains("-") && !word.StartsWith("-") && !word.EndsWith("-") && !IsCommonWord(word))
                    {
                        parameters["appName"] = word;
                        break;
                    }
                }
            }
            
            return parameters;
        }
        
        private bool IsCommonWord(string word)
        {
            var commonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "get", "list", "of", "roles", "assigned", "to", "for", "in", "with", "the", "a", "an", 
                "and", "or", "but", "show", "display", "find", "all", "application", "applications",
                "role", "user", "users", "from", "by", "on", "at", "is", "are", "was", "were",
                "associate", "associated", "management", "admin", "system"
            };
            
            return commonWords.Contains(word.ToLower());
        }

        private Dictionary<string, string> ExtractRoleAssignmentParameters(string message)
        {
            var parameters = new Dictionary<string, string>();
            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Look for patterns like "assign [role] to [user] in [app]"
            for (int i = 0; i < words.Length - 1; i++)
            {
                if (words[i].ToLower() == "role" && i + 1 < words.Length)
                {
                    parameters["roleName"] = words[i + 1];
                }
                if (words[i].ToLower() == "user" && i + 1 < words.Length)
                {
                    parameters["username"] = words[i + 1];
                }
                if ((words[i].ToLower() == "app" || words[i].ToLower() == "application") && i + 1 < words.Length)
                {
                    parameters["appName"] = words[i + 1];
                }
                if (words[i].ToLower() == "to" && i + 1 < words.Length)
                {
                    parameters["username"] = words[i + 1];
                }
            }
            
            return parameters;
        }

        private async Task<string> CallMCPToolAsync(string toolName, Dictionary<string, string> parameters, string userMessage)
        {
            try
            {
                Console.WriteLine($"[MCPClientService] Calling MCP tool: {toolName} with AUTHENTICATED RealMCPClientService");
                
                switch (toolName)
                {
                    case "GetB2CUsers":
                        if (parameters.TryGetValue("action", out var action))
                        {
                            parameters.TryGetValue("roleName", out var roleName);
                            parameters.TryGetValue("appName", out var appName);
                            Console.WriteLine($"[MCPClientService] Calling GetB2CUsers with action: {action}, roleName: {roleName}, appName: {appName}");
                            return await _realMCPClient.CallGetB2CUsersToolAsync(action, roleName, appName);
                        }
                        else
                        {
                            Console.WriteLine($"[MCPClientService] GetB2CUsers called without action, defaulting to 'all'");
                            return await _realMCPClient.CallGetB2CUsersToolAsync("all");
                        }

                    #region Legacy Support (Deprecated)
                    case "GetADB2CUser":
                        Console.WriteLine($"[MCPClientService] DEPRECATED: GetADB2CUser called, redirecting to GetB2CUsers with action=all");
                        return await _realMCPClient.CallGetB2CUsersToolAsync("all");

                    case "GetADB2CUsersByAppRole":
                        if (parameters.TryGetValue("appRole", out var legacyAppRole) && parameters.TryGetValue("appName", out var legacyAppName))
                        {
                            Console.WriteLine($"[MCPClientService] DEPRECATED: GetADB2CUsersByAppRole called, redirecting to GetB2CUsers with action=by-role");
                            return await _realMCPClient.CallGetB2CUsersToolAsync("by-role", legacyAppRole, legacyAppName);
                        }
                        else
                        {
                            return "Error: GetADB2CUsersByAppRole requires appRole and appName parameters.";
                        }
                    #endregion

                    case "GetB2CApplications":
                        {
                            bool ownedOnly = false;
                            if (parameters.TryGetValue("ownedOnly", out var ownedOnlyStr))
                            {
                                bool.TryParse(ownedOnlyStr, out ownedOnly);
                            }
                            Console.WriteLine($"[MCPClientService] Calling GetB2CApplications with ownedOnly: {ownedOnly}");
                            return await _realMCPClient.CallGetB2CApplicationsToolAsync(ownedOnly);
                        }

                    case "GetADB2CApplicationRole":
                        if (parameters.TryGetValue("appName", out var roleAppName))
                        {
                            Console.WriteLine($"[MCPClientService] Calling ManageRoles with action get-roles for app: {roleAppName}");
                            return await _realMCPClient.CallManageRolesToolAsync("get-roles", roleAppName);
                        }
                        return "Please specify the application name. Example: 'Show roles for app MyApp'";

                    case "ManageRoles":
                        if (parameters.TryGetValue("action", out var manageAction))
                        {
                            parameters.TryGetValue("appName", out var manageAppName);
                            parameters.TryGetValue("username", out var manageUsername);
                            parameters.TryGetValue("roleName", out var manageRoleName);
                            
                            Console.WriteLine($"[MCPClientService] Calling ManageRoles with action: {manageAction}");
                            return await _realMCPClient.CallManageRolesToolAsync(manageAction, manageAppName, manageUsername, manageRoleName);
                        }
                        return "Please specify the action. Available actions: get-roles, assign-role, revoke-role, get-user-roles, create-role";

                    case "AssignRoletoUser":
                        if (parameters.TryGetValue("username", out var username) && 
                            parameters.TryGetValue("appName", out var assignAppName) && 
                            parameters.TryGetValue("roleName", out var assignRoleName))
                        {
                            Console.WriteLine($"[MCPClientService] Calling ManageRoles with action assign-role: {assignRoleName} to {username} in {assignAppName}");
                            return await _realMCPClient.CallManageRolesToolAsync("assign-role", assignAppName, username, assignRoleName);
                        }
                        return "Please specify username, app name, and role name. Example: 'Assign role Admin to user john@example.com in app MyApp'";

                    case "RevokeUserRole":
                        if (parameters.TryGetValue("username", out var revokeUsername) && 
                            parameters.TryGetValue("appName", out var revokeAppName) && 
                            parameters.TryGetValue("roleName", out var revokeRoleName))
                        {
                            Console.WriteLine($"[MCPClientService] Calling ManageRoles with action revoke-role: {revokeRoleName} from {revokeUsername} in {revokeAppName}");
                            return await _realMCPClient.CallManageRolesToolAsync("revoke-role", revokeAppName, revokeUsername, revokeRoleName);
                        }
                        return "Please specify username, app name, and role name. Example: 'Revoke role Admin from user john@example.com in app MyApp'";

                    case "GetUserRolesFromAllApplications":
                        if (parameters.TryGetValue("username", out var userRolesUsername))
                        {
                            Console.WriteLine($"[MCPClientService] Calling ManageRoles with action get-user-roles for user: {userRolesUsername}");
                            return await _realMCPClient.CallManageRolesToolAsync("get-user-roles", null, userRolesUsername);
                        }
                        return "Please specify the username. Example: 'Get all roles for user john@example.com'";

                    case "CreateAppRole":
                        if (parameters.TryGetValue("appName", out var createAppName) && 
                            parameters.TryGetValue("appRole", out var createAppRole))
                        {
                            Console.WriteLine($"[MCPClientService] Calling ManageRoles with action create-role: {createAppRole} for app {createAppName}");
                            return await _realMCPClient.CallManageRolesToolAsync("create-role", createAppName, null, createAppRole);
                        }
                        return "Please specify both app name and role name. Example: 'Create role Manager for app MyApp'";

                    case "GetWeatherAuthAPI":
                        Console.WriteLine($"[MCPClientService] Calling GetWeatherAuthAPI");
                        return await _realMCPClient.CallGetWeatherForecastToolAsync();

                    default:
                        return $"Unknown tool: {toolName}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MCPClientService] Error calling MCP tool {toolName}: {ex.Message}");
                return $"Error calling MCP tool {toolName}: {ex.Message}";
            }
        }

        private async Task<string> SimulateMCPToolCall(string email)
        {
            // For demo purposes, simulate MCP tool response
            // In a real implementation, this would connect to the actual MCP server
            await Task.Delay(500); // Simulate network call
            
            var simulatedProfile = new
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = GetDisplayNameFromEmail(email),
                GivenName = GetFirstNameFromEmail(email),
                Surname = "Doe",
                Email = email,
                JobTitle = "Software Developer",
                Department = "Engineering",
                OfficeLocation = "Seattle, WA",
                MobilePhone = "+1 (555) 123-4567",
                BusinessPhones = new[] { "+1 (555) 987-6543" }
            };

            return JsonSerializer.Serialize(simulatedProfile, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }

        private async Task<string> SimulateCurrentUserProfileCall()
        {
            // For demo purposes, simulate current user profile
            await Task.Delay(500); // Simulate network call
            
            var simulatedProfile = new
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "Current User",
                GivenName = "Service",
                Surname = "Account",
                Email = "service@company.com",
                JobTitle = "Service Account",
                Department = "IT",
                OfficeLocation = "Cloud",
                MobilePhone = "N/A",
                BusinessPhones = new string[] { }
            };

            return JsonSerializer.Serialize(simulatedProfile, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }

        private string GetDisplayNameFromEmail(string email)
        {
            var namePart = email.Split('@')[0];
            var parts = namePart.Split('.', '_', '-');
            return string.Join(" ", parts.Select(p => char.ToUpper(p[0]) + p.Substring(1).ToLower()));
        }

        private string GetFirstNameFromEmail(string email)
        {
            var namePart = email.Split('@')[0];
            var firstPart = namePart.Split('.', '_', '-')[0];
            return char.ToUpper(firstPart[0]) + firstPart.Substring(1).ToLower();
        }

        public void ClearConversation()
        {
            // Keep only the system message
            _conversationHistory.Clear();
            _conversationHistory.Add(new SystemChatMessage(@"You are a helpful AI assistant with access to Azure AD B2C management tools through MCP. 
                
                Available MCP Tools:
                1. GetB2CUsers(action, roleName, appName) - Unified user management tool with actions:
                   - all: Gets all Azure AD B2C users (no additional parameters needed)
                   - by-role: Gets users by application role (requires roleName and appName)
                2. GetB2CApplications(ownedOnly) - Gets Azure AD B2C applications (ownedOnly: false=all apps, true=only owned apps)
                3. ManageRoles(action, appName, username, roleName) - Unified role management tool with actions:
                   - get-roles: Gets roles for a specific application (requires appName)
                   - assign-role: Assigns a role to a user (requires username, appName, roleName)
                   - revoke-role: Revokes a role from a user (requires username, appName, roleName)
                   - get-user-roles: Gets all roles for a user across applications (requires username)
                   - create-role: Creates a new role for an application (requires appName, roleName)
                4. GetWeatherAuthAPI() - Gets weather forecast (demo tool)
                
                When users ask questions, determine which tool(s) to use based on their request:
                - User management: Use GetB2CUsers with appropriate action:
                  * For all users: action=all
                  * For users by role: action=by-role, roleName=<role>, appName=<app>
                - Application management: GetB2CApplications (use ownedOnly=false for all applications, ownedOnly=true for owned applications)
                - Role management: Use ManageRoles with appropriate action:
                  * For getting application roles: action=get-roles, appName=<app name>
                  * For assigning roles: action=assign-role, username=<user>, appName=<app>, roleName=<role>
                  * For revoking roles: action=revoke-role, username=<user>, appName=<app>, roleName=<role>
                  * For user role overview: action=get-user-roles, username=<user>
                  * For creating new roles: action=create-role, appName=<app>, roleName=<role>
                - Weather: GetWeatherAuthAPI
                
                Always format results as HTML tables or well-structured content for better readability.
                If you need parameters for a tool, ask the user to provide them."));
        }

        public int GetConversationMessageCount()
        {
            return _conversationHistory.Count - 1; // Exclude system message from count
        }
    }

    public class MCPToolRequest
    {
        public bool ShouldCallTool { get; set; }
        public string ToolName { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }
}
