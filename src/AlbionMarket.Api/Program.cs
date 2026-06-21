using AlbionMarket.Application.DependencyInjection;
using AlbionMarket.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddApplication()
    .AddInfrastructure();

var app = builder.Build();

// 🔥 Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    app = "MarketForge API",
    version = "1.2.0",
    status = "online",
    health = "/health"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    app = "MarketForge API",
    version = "1.2.0",
    utc = DateTimeOffset.UtcNow
}));

app.MapControllers();

app.Run();