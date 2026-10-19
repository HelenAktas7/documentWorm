using System.Threading;
using System.Threading.Tasks;
using DocumentWorm.Core.DTOs;
using DocumentWorm.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DocumentWorm.Api.Endpoints;

public static class ChatEndpoints
{
    public static RouteGroupBuilder MapChatEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/query", async (
            ChatQueryRequestDto request,
            IRagService ragService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Question))
            {
                return Results.BadRequest(new { error = "Soru alanı boş olamaz." });
            }

            var response = await ragService.AskAsync(request, ct);
            return Results.Ok(response);
        })
        .WithName("QueryDocuments")
        .WithSummary("Dokümanlar üzerinden semantik RAG sorgusu")
        .WithDescription("Kullanıcının sorusunu vektöre dönüştürür, Pgvector üzerinde kosinüs benzerliği ile en alakalı parçaları bulur ve Gemini ile doğrulanmış, kaynak atıflı cevap üretir.");

        return group;
    }
}
