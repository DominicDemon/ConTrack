using Contrack.Application;
using Contrack.Infrastructure;
using Contrack.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var allowedOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(options => options.AddPolicy("web", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // OpenAPI JSON: /openapi/v1.json (frontend sinh client từ đây) ; Swagger UI: /swagger
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "ConTrack API v1"));
}

app.UseCors("web");

// Kiểm tra môi trường: API chạy và kết nối được Master DB hay chưa.
app.MapGet("/api/health", async (MasterDbContext db, CancellationToken ct) =>
{
    var masterDbConnected = await db.Database.CanConnectAsync(ct);
    return Results.Ok(new
    {
        status = "ok",
        masterDb = masterDbConnected ? "connected" : "unreachable",
        time = DateTimeOffset.UtcNow
    });
}).WithName("GetHealth");

app.Run();

// Cho phép WebApplicationFactory<Program> dùng trong IntegrationTests.
public partial class Program { }
