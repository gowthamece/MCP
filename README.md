# Azure AD B2C MCP-FullStack

A complete **Model Context Protocol (MCP)** implementation using **.NET** with **Microsoft Graph API** and **Azure AD B2C** integration. This project demonstrates a full-stack architecture with a Blazor client, MCP Server, and real Microsoft Graph API connectivity with Azure AD B2C authentication.

## 🏗️ Architecture

```
┌─────────────────┐    HTTP/MCP     ┌──────────────────┐    Graph API    ┌─────────────────┐
│   Blazor Client │ ────────────► │    MCP Server    │ ─────────────► │ Microsoft Graph │
│   (Port 5017)   │               │   (Port 5156)    │                │      API        │
└─────────────────┘               └──────────────────┘                └─────────────────┘
         │                                   │
         │ Azure OpenAI                      │ Azure AD B2C
         ▼                                   ▼
┌─────────────────┐                ┌──────────────────┐
│   GPT-4 LLM     │                │   Authentication │
│   Reasoning     │                │   & User Data    │
└─────────────────┘                └──────────────────┘
```

## 🚀 Features

- **Real MCP Server**: Implements Microsoft Graph API with Azure AD B2C authentication
- **Blazor Client**: ChatGPT-style UI with Azure OpenAI GPT-4 integration
- **Azure AD B2C Integration**: Enterprise-grade identity management and authentication
- **HTTP-based Communication**: Custom HTTP API for MCP protocol communication
- **Microsoft Graph Integration**: Real user profile data from Azure AD B2C
- **Comprehensive Logging**: Detailed diagnostic logs for troubleshooting
- **Fallback Simulation**: Graceful fallback to simulated data when API calls fail

## 📦 Projects

### MCP.ADB2C.Client (Blazor Client)
- **Framework**: Blazor Server (.NET 8)
- **AI Integration**: Azure OpenAI GPT-4.1
- **UI**: Interactive server-side rendering with SignalR
- **Authentication**: Azure AD B2C with OpenID Connect
- **MCP Client**: HTTP-based communication to MCP Server
- **Port**: 5017

### MCP.ADB2C (MCP Server)
- **Framework**: ASP.NET Core (.NET 8)
- **MCP Protocol**: MCP.NET.Server v0.9.0
- **API Integration**: Microsoft Graph API v5.90.0
- **Authentication**: Azure AD B2C with ClientSecret credentials
- **Port**: 5156 (HTTP API)

### MCP-Balzor-AI-App.ServiceDefaults
- **Framework**: .NET 8
- **Purpose**: Shared service defaults and extensions

### MCP-Balzor-AI-App.AppHost
- **Framework**: .NET Aspire (.NET 8)
- **Purpose**: Application orchestration and service discovery

## 🛠️ Setup Instructions

### Prerequisites
- **.NET 8 SDK** (required for both client and server)
- **Azure Account** with Active Directory setup
- **Azure OpenAI** service access
- **Visual Studio 2022** or **VS Code** (recommended)

### 1. Clone the Repository
```bash
git clone <repository-url>
cd MCP-Balzor-AI-App
```

### 2. Azure AD B2C Configuration
Configure your Azure AD B2C tenant:

1. **Create Azure AD B2C tenant**
2. **Register Applications**:
   - Client Application (for Blazor app)
   - Server Application (for MCP Server)
3. **Configure Authentication**:
   - Set redirect URIs for the client
   - Grant necessary Graph API permissions
4. **Update Configuration Files** (see Configuration section below)

### 3. Configuration

#### MCP.ADB2C.Client Configuration
Update `appsettings.json` in the `MCP.ADB2C.Client` project:

```json
{
  "AzureAd": {
    "Instance": "https://<your-tenant>.b2clogin.com/",
    "Domain": "<your-tenant>.onmicrosoft.com",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<your-blazor-client-id>",
    "ClientSecret": "<your-blazor-client-secret>",
    "CallbackPath": "/signin-oidc",
    "SignUpSignInPolicyId": "B2C_1_signupsignin1"
  },
  "Azure": {
    "OpenAI": {
      "Endpoint": "https://<your-openai-resource>.openai.azure.com/",
      "ApiKey": "<your-openai-api-key>",
      "Model": "gpt-4"
    }
  },
  "MCPClient": {
    "BaseUrl": "https://localhost:5156",
    "TimeoutSeconds": 30
  }
}
```

#### MCP.ADB2C Server Configuration
Update `appsettings.json` in the `MCP.ADB2C` project:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<your-server-client-id>",
    "ClientSecret": "<your-server-client-secret>"
  },
  "GraphApi": {
    "BaseUrl": "https://graph.microsoft.com",
    "Scopes": ["https://graph.microsoft.com/.default"]
  }
}
```

### 4. Build and Run

#### Option A: Using .NET Aspire (Recommended)
```bash
# Navigate to the AppHost project
cd MCP-Balzor-AI-App.AppHost

# Run with Aspire orchestration
dotnet run
```
This will start:
- **MCP.ADB2C.Client** on port **5017**
- **MCP.ADB2C** on port **5156**

#### Option B: Manual Startup
```bash
# Terminal 1: Start MCP Server
cd MCP.ADB2C
dotnet run

# Terminal 2: Start Blazor Client  
cd MCP.ADB2C.Client
dotnet run
```

### 5. Access the Application
- **Blazor Client**: `https://localhost:5017`
- **MCP Server API**: `https://localhost:5156`
- **Aspire Dashboard**: `https://localhost:15888` (when using Aspire)

## 💡 Usage

1. Navigate to `https://localhost:5017`
2. Go to the "Azure AD B2C AI Assistant" page
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
- Handles Microsoft Graph API authentication with Azure AD B2C
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
**Solution**: Use a valid user email from your Azure AD B2C tenant.

### Authentication Errors
**Solution**: Verify Azure AD B2C app registration permissions and admin consent.

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
