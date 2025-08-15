using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MCP_Balzor_AI_App.Services
{
    public class MCPClientService
    {
        private readonly AzureOpenAIClient _client;
        private readonly string _deploymentName;
        private readonly List<ChatMessage> _conversationHistory;
        private readonly HttpClient _httpClient;
        private readonly RealMCPClientService _realMCPClient;

        public MCPClientService(IConfiguration configuration, HttpClient httpClient, RealMCPClientService realMCPClient)
        {
            var endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint");
            var key = configuration["AzureOpenAI:Key"] ?? throw new ArgumentNullException("AzureOpenAI:Key");
            _deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? throw new ArgumentNullException("AzureOpenAI:DeploymentName");
            _client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key));
            _httpClient = httpClient;
            _realMCPClient = realMCPClient;
            
            // Initialize conversation with system message that includes MCP tool awareness
            _conversationHistory = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are a helpful AI assistant with access to Microsoft Graph data through MCP tools. 
                When users ask for user profile information or mention 'pull user details', you should:
                1. Look for email addresses in their request
                2. Use the get_user_profile tool to fetch their details
                3. Present the information in a well-formatted table
                
                Available tools:
                - get_user_profile(email): Gets user profile by email address
                - get_current_user_profile(): Gets current authenticated user profile
                
                When you receive user profile data, always format it as an HTML table for better readability.")
            };
        }

        public async Task<string> GetChatCompletionAsync(string userMessage)
        {
            var chatClient = _client.GetChatClient(_deploymentName);
            
            Console.WriteLine($"[MCPClientService] Processing message: {userMessage}");
            
            // Add user message to conversation history
            _conversationHistory.Add(new UserChatMessage(userMessage));

            // Check if the user is asking for user profile information
            var shouldCallTool = await ShouldCallMCPToolAsync(userMessage);
            Console.WriteLine($"[MCPClientService] Should call MCP tool: {shouldCallTool}");
            
            if (shouldCallTool)
            {
                var mcpResult = await CallMCPToolAsync(userMessage);
                if (!string.IsNullOrEmpty(mcpResult))
                {
                    // Add the MCP result as context for the AI
                    var contextMessage = new SystemChatMessage($"MCP Tool Result: {mcpResult}");
                    _conversationHistory.Add(contextMessage);
                    
                    // Add instruction to format as table
                    _conversationHistory.Add(new SystemChatMessage("Please format the user profile information above as a nice HTML table with proper styling."));
                }
            }

            var response = await chatClient.CompleteChatAsync(_conversationHistory);
            var assistantResponse = response.Value.Content[0].Text;
            
            // Add assistant response to conversation history
            _conversationHistory.Add(new AssistantChatMessage(assistantResponse));
            
            return assistantResponse;
        }

        private async Task<bool> ShouldCallMCPToolAsync(string userMessage)
        {
            // Check for keywords that indicate user profile request
            var keywords = new[] { "user details", "user profile", "pull user", "get user", "user information" };
            var message = userMessage.ToLowerInvariant();
            
            Console.WriteLine($"[MCPClientService] Checking message: '{message}'");
            
            var hasKeywords = keywords.Any(keyword => message.Contains(keyword));
            var hasEmail = Regex.IsMatch(message, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
            
            Console.WriteLine($"[MCPClientService] Has keywords: {hasKeywords}, Has email: {hasEmail}");
            
            return hasKeywords || hasEmail;
        }

        private async Task<string> CallMCPToolAsync(string userMessage)
        {
            try
            {
                Console.WriteLine($"[MCPClientService] Calling MCP tool for message: {userMessage}");
                
                // Connect to MCP Server if not already connected
                await _realMCPClient.ConnectToMCPServerAsync();

                // Extract email from the message
                var emailMatch = Regex.Match(userMessage, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
                
                if (emailMatch.Success)
                {
                    var email = emailMatch.Value;
                    Console.WriteLine($"[MCPClientService] Found email: {email}, calling GetUserProfile");
                    return await _realMCPClient.CallGetUserProfileToolAsync(email);
                }
                else
                {
                    // If no email found, try to get current user profile
                    Console.WriteLine($"[MCPClientService] No email found, calling GetCurrentUserProfile");
                    return await _realMCPClient.CallGetCurrentUserProfileToolAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MCPClientService] Error calling MCP tool: {ex.Message}");
                return $"Error calling MCP tool: {ex.Message}";
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
            _conversationHistory.Add(new SystemChatMessage(@"You are a helpful AI assistant with access to Microsoft Graph data through MCP tools. 
                When users ask for user profile information or mention 'pull user details', you should:
                1. Look for email addresses in their request
                2. Use the get_user_profile tool to fetch their details
                3. Present the information in a well-formatted table
                
                Available tools:
                - get_user_profile(email): Gets user profile by email address
                - get_current_user_profile(): Gets current authenticated user profile
                
                When you receive user profile data, always format it as an HTML table for better readability."));
        }

        public int GetConversationMessageCount()
        {
            return _conversationHistory.Count - 1; // Exclude system message from count
        }
    }
}
