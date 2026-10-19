using System;
using System.Threading;
using System.Threading.Tasks;
using DocumentWorm.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DocumentWorm.Api.Endpoints;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/health", async (DocumentWormDbContext db, CancellationToken ct) =>
        {
            bool dbConnected = false;
            string dbStatus = "Unknown";
            int totalDocuments = 0;
            int totalChunks = 0;

            try
            {
                dbConnected = await db.Database.CanConnectAsync(ct);
                if (dbConnected)
                {
                    totalDocuments = await db.Documents.CountAsync(ct);
                    totalChunks = await db.DocumentChunks.CountAsync(ct);
                    dbStatus = "Connected (pgvector ready)";
                }
            }
            catch (Exception ex)
            {
                dbStatus = $"Error: {ex.Message}";
            }

            return Results.Ok(new
            {
                status = dbConnected ? "Healthy" : "Degraded",
                service = "DocumentWorm AI Engine",
                database = dbStatus,
                totalDocuments,
                totalChunks,
                serverTimeUtc = DateTime.UtcNow
            });
        })
        .WithName("HealthCheck")
        .WithSummary("Sistem sağlık ve veritabanı durumu");

        return group;
    }
}
