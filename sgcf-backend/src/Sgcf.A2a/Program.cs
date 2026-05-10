// Sgcf.A2a -- Agent-to-Agent (A2A) protocol server entry point.
// A2A tool registrations will be added incrementally.

using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var app = builder.Build();

await app.RunAsync();
