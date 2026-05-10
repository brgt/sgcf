// Sgcf.Mcp -- MCP (Model Context Protocol) server entry point.
// MCP tool registrations will be added incrementally.

using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var app = builder.Build();

await app.RunAsync();
