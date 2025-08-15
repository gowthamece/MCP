# MCP Client-Server Execution Sequence

## Complete Program Flow

### 🚀 **Step 1: Start MCP Server**
```bash
# Terminal 1 - Start MCP Server
cd MCP-Balzor-AI-App.MCPServer
dotnet run
```

**What happens:**
1. `Program.cs` creates host with `MCPServerService`
2. `MCPServerService.ExecuteAsync()` starts
3. Creates `McpServer` with server info
4. `MCPToolsService.RegisterToolsAsync()` registers tools:
   - `get_user_profile` 
   - `get_current_user_profile`
5. Server listens on port 8080
6. Logs: "MCP Server started successfully"

### 🌐 **Step 2: Start Blazor App (MCP Client)**
```bash
# Terminal 2 - Start Blazor App  
cd MCP-Balzor-AI-App
dotnet run
```

**What happens:**
1. `Program.cs` registers services:
   - `HttpMCPClientService` (for real MCP calls)
   - `MCPClientService` (main chat service)
2. Blazor app starts on http://localhost:5017
3. User navigates to chat page

### 💬 **Step 3: User Interaction**
```
User Input: "pull user details for john.doe@contoso.com"
```

**Execution Flow:**

#### **3.1 Frontend (MCPChat.razor)**
```csharp
private async Task SendMessage()
{
    // Add user message to chat
    await _mcpClientService.GetChatCompletionAsync(userMessage);
}
```

#### **3.2 Chat Service (MCPClientService.cs)**
```csharp
public async Task<string> GetChatCompletionAsync(string userMessage)
{
    // 1. Check if message needs MCP tool
    if (ContainsMCPToolRequest(userMessage))
    {
        // 2. Call MCP tool
        var mcpResponse = await CallMCPToolAsync(userMessage);
        
        // 3. Send to Azure OpenAI with MCP data
        var messages = new[]
        {
            new SystemChatMessage("You have MCP tool response: " + mcpResponse),
            new UserChatMessage(userMessage)
        };
        
        // 4. Get AI response
        var response = await _client.GetChatClient(_deploymentName)
            .CompleteChatAsync(messages);
            
        return response.Value.Content[0].Text;
    }
}
```

#### **3.3 MCP Tool Detection**
```csharp
private bool ContainsMCPToolRequest(string message)
{
    // Detects keywords: "pull", "user details", "profile", email patterns
    var keywords = new[] { "pull", "user details", "profile", "get user" };
    return keywords.Any(k => message.Contains(k)) || 
           Regex.IsMatch(message, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
}
```

#### **3.4 MCP Client Call (HttpMCPClientService.cs)**
```csharp
private async Task<string> CallMCPToolAsync(string userMessage)
{
    // 1. Extract email from message
    var emailMatch = Regex.Match(userMessage, @"email_pattern");
    
    if (emailMatch.Success)
    {
        // 2. Call HTTP MCP Client
        return await _httpMCPClient.CallGetUserProfileToolAsync(email);
    }
}
```

#### **3.5 HTTP Call to MCP Server**
```csharp
public async Task<string> CallGetUserProfileToolAsync(string email)
{
    // 1. Create HTTP request
    var request = new
    {
        tool = "get_user_profile",
        arguments = new { email = email }
    };
    
    // 2. POST to MCP Server
    var response = await _httpClient.PostAsync(
        "http://localhost:8080/mcp/tools/call", 
        content);
        
    // 3. Return response or fallback
    return response.IsSuccessStatusCode 
        ? await response.Content.ReadAsStringAsync()
        : await FallbackGetUserProfileAsync(email);
}
```

### 🔧 **Step 4: MCP Server Processing**

#### **4.1 Tool Execution (MCPToolsService.cs)**
```csharp
private async Task<ToolCallResult> ExecuteGetUserProfileAsync(JsonElement? arguments)
{
    // 1. Extract email from arguments
    var email = arguments.Value.GetProperty("email").GetString();
    
    // 2. Call GraphService
    var profileData = await _graphService.GetUserProfileAsync(email);
    
    // 3. Return structured response
    return new ToolCallResult
    {
        Content = new ContentBase[]
        {
            new TextContent { Text = $"User Profile for {email}:\n\n{profileData}" }
        }
    };
}
```

#### **4.2 Microsoft Graph Simulation (GraphService.cs)**
```csharp
public async Task<string> GetUserProfileAsync(string userEmail)
{
    // 1. Log the request
    _logger.LogInformation("Simulating user profile retrieval for: {Email}", userEmail);
    
    // 2. Create simulated profile
    var simulatedProfile = new
    {
        Id = Guid.NewGuid().ToString(),
        DisplayName = GetSimulatedDisplayName(userEmail),
        Mail = userEmail,
        JobTitle = "Software Developer",
        // ... other properties
    };
    
    // 3. Return JSON
    return JsonSerializer.Serialize(simulatedProfile, new JsonSerializerOptions { WriteIndented = true });
}
```

### 📊 **Step 5: Response Flow**

#### **5.1 Data flows back through the chain:**
```
MCP Server → HTTP Response → HttpMCPClientService → MCPClientService → Azure OpenAI → MCPChat.razor
```

#### **5.2 AI Processing**
- Azure OpenAI receives the user profile data
- Formats it into a nice HTML table
- Returns conversational response

#### **5.3 UI Display**
- `MCPChat.razor` receives the AI response
- Renders HTML table with user profile
- Displays in chat interface

## 🔧 **How to Test This Flow:**

### Terminal 1 (MCP Server):
```bash
cd MCP-Balzor-AI-App.MCPServer
dotnet run
```

### Terminal 2 (Blazor App):
```bash
cd MCP-Balzor-AI-App  
dotnet run
```

### Test Messages:
1. "pull user details for john.doe@contoso.com"
2. "get profile for jane.smith@company.com"
3. "show current user information"

## 📝 **Sequence Summary:**

1. **MCP Server starts** and registers tools
2. **Blazor app starts** and connects to Azure OpenAI
3. **User sends message** requesting user profile
4. **MCPClientService detects** MCP tool request
5. **HttpMCPClientService makes** HTTP call to MCP Server
6. **MCP Server executes** get_user_profile tool
7. **GraphService returns** simulated user data
8. **Response flows back** to Blazor app
9. **Azure OpenAI formats** the data nicely
10. **UI displays** the result in chat

This gives you a complete **end-to-end MCP integration** with proper separation of concerns! 🎉
