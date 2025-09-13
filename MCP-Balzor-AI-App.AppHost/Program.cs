var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.MCP_ADB2C_Client>("mcp-adb2c-client");

builder.AddProject<Projects.MCP_ADB2C>("mcp-adb2c");

builder.Build().Run();
