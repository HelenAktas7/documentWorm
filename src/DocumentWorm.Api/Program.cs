using System;
using System.Linq;
using System.Threading.RateLimiting;
using DocumentWorm.Api.Endpoints;
using DocumentWorm.Api.Middleware;
using DocumentWorm.Infrastructure;
using DocumentWorm.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Infrastructure Servislerini Ekle (PostgreSQL, Pgvector, Gemini, Parsers, Auth)
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Standart RFC 7807 Hata Yanıtları (Problem Details)
builder.Services.AddProblemDetails();

// 3. .NET 9 OpenAPI Desteği
builder.Services.AddOpenApi();

// 4. ASP.NET Core Yerleşik Hız Sınırlayıcı (Rate Limiting)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc6585#section-4",
            title = "Too Many Requests",
            status = StatusCodes.Status429TooManyRequests,
            detail = "API anahtarı başına dakikalık istek kotası (10 istek/dk) aşıldı. Lütfen bekleyiniz."
        }, cancellationToken: token);
    };

    // API Key veya IP bazlı bölümleme (Partitioning)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var partitionKey = httpContext.Request.Headers["X-API-KEY"].FirstOrDefault()
                           ?? httpContext.Connection.RemoteIpAddress?.ToString()
                           ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

var app = builder.Build();

// 5. Veritabanı ve Pgvector Eklentisini Otomatik Başlatma
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocumentWormDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Veritabanı bağlantısı kontrol ediliyor...");
        if (await db.Database.CanConnectAsync())
        {
            await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector;");
            await db.Database.EnsureCreatedAsync();
            logger.LogInformation("PostgreSQL ve pgvector eklentisi hazır!");
        }
        else
        {
            logger.LogWarning("PostgreSQL henüz ayakta değil. Docker'ı başlattıktan sonra otomatik bağlanacaktır.");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning("Veritabanı başlangıç adımı ertelendi: {Error}", ex.Message);
    }
}

// 6. Middleware Boru Hattı
app.UseExceptionHandler();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("DocumentWorm AI Engine API")
               .WithTheme(ScalarTheme.Moon);
    });
}

// Ana sayfaya (/) girenleri doğrudan Scalar Dokümantasyonuna yönlendir
app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

// 7. Herkese Açık Sağlık Kontrolü Uç Noktası
app.MapGroup("/api").MapHealthEndpoints();

// 8. API Key ile Korunan Uç Noktalar
var apiGroup = app.MapGroup("/api");
apiGroup.AddEndpointFilter<ApiKeyEndpointFilter>();

apiGroup.MapGroup("/documents").MapDocumentEndpoints();
apiGroup.MapGroup("/chat").MapChatEndpoints();
apiGroup.MapGroup("/admin").MapAdminEndpoints();

app.Run();
