using System.Linq;
using System.Threading.Tasks;
using DocumentWorm.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace DocumentWorm.Api.Middleware;

public class ApiKeyEndpointFilter : IEndpointFilter
{
    private const string ApiKeyHeaderName = "X-API-KEY";
    private readonly IApiKeyValidator _apiKeyValidator;

    public ApiKeyEndpointFilter(IApiKeyValidator apiKeyValidator)
    {
        _apiKeyValidator = apiKeyValidator;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, 
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Dokümantasyon, Health ve İlk Anahtar Oluşturma uç noktalarını yetkilendirmeden muaf tutuyoruz
        var path = httpContext.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (path.StartsWith("/scalar") ||
            path.StartsWith("/openapi") ||
            path.StartsWith("/api/health") ||
            path.StartsWith("/api/admin/setup-first-key"))
        {
            return await next(context);
        }

        // İstekte 'X-API-KEY' başlığı var mı?
        if (!httpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey) ||
            string.IsNullOrWhiteSpace(extractedApiKey.FirstOrDefault()))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: $"'{ApiKeyHeaderName}' başlığı eksik! Lütfen geçerli bir API anahtarı sağlayınız."
            );
        }

        // Anahtar veritabanında kayıtlı ve aktif mi?
        var client = await _apiKeyValidator.ValidateAsync(extractedApiKey.ToString()!);
        if (client == null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Geçersiz veya pasif API Anahtarı. Erişim reddedildi."
            );
        }

        // İleride rate limiter veya loglar kullansın diye istemciyi HttpContext'e ekle
        httpContext.Items["ApiClient"] = client;

        return await next(context);
    }
}
