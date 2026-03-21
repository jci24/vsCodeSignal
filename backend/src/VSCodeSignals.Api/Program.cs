using FastEndpoints;
using VSCodeSignals.Api.App.Host;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFeatureHandlers();
builder.Services.AddFastEndpoints();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.UseFastEndpoints();

app.Run();

public partial class Program;
