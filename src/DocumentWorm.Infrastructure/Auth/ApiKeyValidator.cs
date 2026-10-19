using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentWorm.Core.Entities;
using DocumentWorm.Core.Interfaces;
using DocumentWorm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DocumentWorm.Infrastructure.Auth;

public class ApiKeyValidator : IApiKeyValidator
{
    private readonly DocumentWormDbContext _dbContext;

    public ApiKeyValidator(DocumentWormDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Gelen ham API anahtarını hash'leyip veritabanındakiyle karşılaştırır
    public async Task<ApiClient?> ValidateAsync(string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var hash = ComputeHash(apiKey.Trim());
        return await _dbContext.ApiClients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ApiKeyHash == hash && c.IsActive, ct);
    }

    // Yeni bir istemci için güvenli API anahtarı üretir
    public async Task<string> GenerateApiKeyAsync(string clientName, int rateLimitPerMinute = 10, CancellationToken ct = default)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var randomString = Convert.ToHexString(randomBytes).ToLowerInvariant();
        var rawApiKey = $"dw_live_{randomString}";
        var hash = ComputeHash(rawApiKey);
        var prefix = rawApiKey.Substring(0, 12);

        var client = new ApiClient
        {
            ClientName = clientName,
            ApiKeyHash = hash,
            ApiKeyPrefix = prefix,
            RateLimitPerMinute = rateLimitPerMinute,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.ApiClients.AddAsync(client, ct);
        await _dbContext.SaveChangesAsync(ct);

        // Ham anahtar yalnızca bir kez kullanıcıya gösterilir
        return rawApiKey;
    }

    public static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
