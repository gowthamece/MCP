var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.MCP_Balzor_AI_App>("mcp-balzor-ai-app");

builder.Build().Run();
