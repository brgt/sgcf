// Sgcf.Jobs -- Background jobs host entry point.
// Job registrations will be added incrementally.

using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var app = builder.Build();

await app.RunAsync();
