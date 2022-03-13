using BackendService.Services;
using Google.Cloud.Diagnostics.Common;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

if (builder.Environment.IsProduction())
{
    builder.Logging.AddGoogle();
}

// Add services to the container.
builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");


// Google Cloud Run will set to to the port that we are supposed to listen on.
// It will typically be 8080.
string? port = Environment.GetEnvironmentVariable("PORT");
if (port == null)
{
    app.Run();
}
else
{
    var url = $"http://0.0.0.0:{port}";
    app.Run(url);
}
