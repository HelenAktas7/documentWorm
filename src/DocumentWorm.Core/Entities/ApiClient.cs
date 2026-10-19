using System;

namespace DocumentWorm.Core.Entities;

public class ApiClient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ClientName { get; set; } = string.Empty;
    public string ApiKeyHash { get; set; } = string.Empty;
    public string ApiKeyPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int RateLimitPerMinute { get; set; } = 10;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
