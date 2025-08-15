namespace MCP_Balzor_AI_App.MCPServer.Services
{
    public interface IGraphService
    {
        Task<string> GetUserProfileAsync(string userEmail);
        Task<string> GetCurrentUserProfileAsync();
    }
}
