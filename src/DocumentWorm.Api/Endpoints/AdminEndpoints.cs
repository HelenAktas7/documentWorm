using System.Threading;
using System.Threading.Tasks;
using DocumentWorm.Core.Interfaces;
using DocumentWorm.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DocumentWorm.Api.Endpoints;

public static class AdminEndpoints
{
    public record CreateKeyRequest(string ClientName, int RateLimitPerMinute = 10);

    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder group)
    {
        // Onboarding: Sisteme ilk defa başlayan kişinin Master API anahtarını almasını sağlar
        group.MapPost("/setup-first-key", async (
            DocumentWormDbContext db,
            IApiKeyValidator validator,
            CancellationToken ct) =>
        {
            var count = await db.ApiClients.CountAsync(ct);
            if (count > 0)
            {
                return Results.BadRequest(new
                {
                    error = "Sistemde zaten kayıtlı API anahtarı bulunmaktadır. Yeni anahtar için '/api/admin/generate-key' uç noktasını kullanınız."
                });
            }

            var key = await validator.GenerateApiKeyAsync("Master Admin Client", rateLimitPerMinute: 60, ct);
            return Results.Ok(new
            {
                message = "İlk API Anahtarı başarıyla oluşturuldu! Lütfen bu anahtarı kaydedin. İsteklerinizde 'X-API-KEY' başlığı olarak gönderiniz.",
                apiKey = key,
                clientName = "Master Admin Client",
                rateLimitPerMinute = 60
            });
        })
        .WithName("SetupFirstApiKey")
        .WithSummary("İlk Master API Anahtarını Oluştur (Onboarding)");

        // Yeni müşteri/departman için anahtar üretme
        group.MapPost("/generate-key", async (
            CreateKeyRequest request,
            IApiKeyValidator validator,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.ClientName))
            {
                return Results.BadRequest(new { error = "ClientName alanı zorunludur." });
            }

            var key = await validator.GenerateApiKeyAsync(request.ClientName, request.RateLimitPerMinute, ct);
            return Results.Ok(new
            {
                message = "Yeni API Anahtarı üretildi.",
                apiKey = key,
                clientName = request.ClientName,
                rateLimitPerMinute = request.RateLimitPerMinute
            });
        })
        .WithName("GenerateApiKey")
        .WithSummary("Yeni İstemci API Anahtarı Üret");

        return group;
    }
}
