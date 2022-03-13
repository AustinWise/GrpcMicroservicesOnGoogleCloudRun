using Google.Cloud.Diagnostics.Common;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

if (builder.Environment.IsProduction())
{
    builder.Logging.AddGoogle();
}

var grpc = builder.Services.AddGrpcClient<GrpcContracts.Greeters.Greeter.GreeterClient>(o =>
{
    o.Address = new Uri(builder.Configuration["Greeter:URI"]);
});

if (builder.Environment.IsProduction())
{
    grpc.ConfigureComputeCredentialAuth();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

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
