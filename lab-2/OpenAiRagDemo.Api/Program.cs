using dotenv;
using dotenv.net;
using Microsoft.EntityFrameworkCore;
using OpenAiRagDemo.Api.Data;
using OpenAiRagDemo.Api.Services;
using OpenAiRagDemo.Api.Services.Interfaces;

// Завантажити .env файл
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure PostgreSQL with EF Core
var connectionString = $"Host={Environment.GetEnvironmentVariable("DATABASE_HOST")};" +
                       $"Port={ Environment.GetEnvironmentVariable("DATABASE_PORT")};" +
                       $"Database={ Environment.GetEnvironmentVariable("DATABASE_NAME")};" +
                       $"Username={ Environment.GetEnvironmentVariable("DATABASE_USER")};" +
                       $"Password={ Environment.GetEnvironmentVariable("DATABASE_PASSWORD")}";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, o => o.UseVector());
});

// Register services
builder.Services.AddSingleton<IFileService, LocalFileService>();
// Реєструємо PDF сервіс
builder.Services.AddSingleton<IPdfService, PdfService>();
// Реєструємо OpenAI сервіс (залежить від PdfService)
builder.Services.AddSingleton<IOpenAiService, OpenAiService>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();