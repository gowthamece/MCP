# MCP-FullStack

A complete **Model Context Protocol (MCP)** implementation using **.NET** with **Microsoft Graph API** integration. This project demonstrates a full-stack architecture with a Blazor client, MCP Server, and real Microsoft Graph API connectivity.

## 🏗️ Architecture

```
┌─────────────────┐    HTTP/MCP     ┌──────────────────┐    Graph API    ┌─────────────────┐
│   Blazor Client │ ────────────► │    MCP Server    │ ─────────────► │ Microsoft Graph │
│   (Port 5017)   │               │ (Port 8080/5000) │                │      API        │
└─────────────────┘               └──────────────────┘                └─────────────────┘
         │                                   │
         │ Azure OpenAI                      │ Azure AD
         ▼                                   ▼
┌─────────────────┐                ┌──────────────────┐
│   GPT-4 LLM     │                │   Authentication │
│   Reasoning     │                │   & User Data    │
└─────────────────┘                └──────────────────┘
```

## 🚀 Features

- **Real MCP Server**: Implements Microsoft Graph API with Azure AD authentication
- **Blazor Client**: ChatGPT-style UI with Azure OpenAI GPT-4 integration
- **HTTP-based Communication**: Custom HTTP API for MCP protocol communication
- **Microsoft Graph Integration**: Real user profile data from Azure AD
- **Comprehensive Logging**: Detailed diagnostic logs for troubleshooting
- **Fallback Simulation**: Graceful fallback to simulated data when API calls fail

## 📦 Projects

### MCP-Balzor-AI-App (Blazor Client)
- **Framework**: Blazor Server (.NET 8)
- **AI Integration**: Azure OpenAI GPT-4.1
- **UI**: Interactive server-side rendering with SignalR
- **MCP Client**: HTTP-based communication to MCP Server

### MCP-Balzor-AI-App.MCPServer (MCP Server)
- **Framework**: ASP.NET Core (.NET 9)
- **MCP Protocol**: MCP.NET.Server v0.9.0
- **API Integration**: Microsoft Graph API v5.90.0
- **Authentication**: Azure AD with ClientSecret credentials
- **Ports**: 8080 (MCP), 5000 (HTTP API)

### MCP-Balzor-AI-App.ServiceDefaults
- **Framework**: .NET 8
- **Purpose**: Shared service defaults and extensions

## 🛠️ Setup Instructions

### Prerequisites
- .NET 8 SDK or later
- Visual Studio 2022 or VS Code
- Azure subscription with:
  - Azure OpenAI service
  - Azure AD tenant
  - App registration with Microsoft Graph permissions

### 1. Clone the Repository
```bash
git clone https://github.com/YourUsername/MCP-FullStack.git
cd MCP-FullStack
```

### 2. Configure Azure OpenAI
Update `MCP-Balzor-AI-App/appsettings.json`:
```json
{
  "AzureOpenAI": {
    "Endpoint": "YOUR_AZURE_OPENAI_ENDPOINT",
    "Key": "YOUR_AZURE_OPENAI_KEY",
    "DeploymentName": "YOUR_DEPLOYMENT_NAME"
  }
}
```

### 3. Configure Microsoft Graph API
Update `MCP-Balzor-AI-App.MCPServer/appsettings.json`:
```json
{
  "GraphApi": {
    "ClientId": "YOUR_AZURE_AD_CLIENT_ID",
    "ClientSecret": "YOUR_AZURE_AD_CLIENT_SECRET", 
    "TenantId": "YOUR_AZURE_AD_TENANT_ID"
  }
}
```

### 4. Azure AD App Registration Setup
1. Go to Azure Portal → Azure Active Directory → App registrations
2. Create a new app registration
3. Add API permissions:
   - Microsoft Graph → Application permissions → User.Read.All
4. Grant admin consent
5. Create a client secret
6. Copy ClientId, ClientSecret, and TenantId to configuration

### 5. Build and Run

**Terminal 1 - Start MCP Server:**
```bash
dotnet run --project MCP-Balzor-AI-App.MCPServer/MCP-Balzor-AI-App.MCPServer.csproj
```

**Terminal 2 - Start Blazor Client:**
```bash
dotnet run --project MCP-Balzor-AI-App/MCP-Balzor-AI-App.csproj
```

### 6. Access the Application
- **Blazor Client**: http://localhost:5017
- **MCP Server API**: http://localhost:5000
- **MCP Protocol**: localhost:8080

## 💡 Usage

1. Navigate to http://localhost:5017
2. Go to the "MCP Chat" page
3. Try these commands:
   - `"get current user profile information"`
   - `"pull the user [email] information"`
   - `"show me user details for [valid-email-in-your-tenant]"`

## 🔧 Key Components

### MCPClientService.cs
- Detects user profile requests using keywords and email patterns
- Integrates with Azure OpenAI for natural language processing
- Makes HTTP calls to MCP Server

### GraphService.cs
- Implements `IGraphService` interface
- Handles Microsoft Graph API authentication with Azure AD
- Provides real user profile data with fallback to simulation

### ToolsController.cs
- Exposes HTTP API endpoints for MCP tool execution
- Routes tool calls to appropriate Graph service methods
- Returns JSON responses for user profile data

### RealMCPClientService.cs
- HTTP-based MCP client implementation
- Makes POST requests to `/api/tools/execute` endpoint
- Handles connection errors with graceful fallback

## 📊 Logging and Diagnostics

The application provides comprehensive logging:

### MCP Server Logs
```
info: GraphService constructor called
info: GraphAPI Configuration: ClientId=EXISTS, ClientSecret=EXISTS, TenantId=EXISTS
info: HTTP API: Executing tool: get_user_profile for email: user@domain.com
info: Calling Microsoft Graph API for user: user@domain.com
info: Returning real user data from Microsoft Graph API
```

### Blazor Client Logs
```
[MCPClientService] Processing message: get user information
[MCPClientService] Found email: user@domain.com, calling GetUserProfile
info: Making HTTP request to MCP Server for get_user_profile
info: Received successful response from MCP Server: 850 characters
```

## 🚨 Troubleshooting

### User Not Found Error
```
Microsoft.Graph.Models.ODataErrors.ODataError: Resource 'user@domain.com' does not exist
```
**Solution**: Use a valid user email from your Azure AD tenant.

### Authentication Errors
**Solution**: Verify Azure AD app registration permissions and admin consent.

### Connection Errors
**Solution**: Ensure both MCP Server and Blazor Client are running on correct ports.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📚 Related Documentation

- [Model Context Protocol (MCP)](https://modelcontextprotocol.io/)
- [Microsoft Graph API](https://docs.microsoft.com/en-us/graph/)
- [Azure OpenAI Service](https://docs.microsoft.com/en-us/azure/cognitive-services/openai/)
- [Blazor Documentation](https://docs.microsoft.com/en-us/aspnet/core/blazor/)

## ⭐ Acknowledgments

- Model Context Protocol team for the MCP specification
- Microsoft Graph team for the comprehensive API
- .NET community for excellent tooling and libraries
