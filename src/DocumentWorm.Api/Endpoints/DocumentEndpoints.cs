using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentWorm.Core.DTOs;
using DocumentWorm.Core.Entities;
using DocumentWorm.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DocumentWorm.Api.Endpoints;

public static class DocumentEndpoints
{
    public static RouteGroupBuilder MapDocumentEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/upload", async (
            IFormFile file,
            IDocumentParser parser,
            ITextChunker chunker,
            IEmbeddingService embeddingService,
            IVectorStore vectorStore) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "Lütfen geçerli bir dosya yükleyiniz." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".pdf", ".md", ".txt" };
            if (!allowedExtensions.Contains(extension))
            {
                return Results.BadRequest(new
                {
                    error = $"Desteklenmeyen dosya türü: '{extension}'. Yalnızca PDF (.pdf), Markdown (.md) ve Düz Metin (.txt) kabul edilir."
                });
            }

            // 1. Dokümanı sayfalara ayır
            IReadOnlyList<ParsedPage> pages;
            using (var stream = file.OpenReadStream())
            {
                pages = await parser.ParseAsync(stream, file.ContentType, file.FileName);
            }

            if (pages.Count == 0)
            {
                return Results.BadRequest(new { error = "Dosyadan metin çıkarılamadı." });
            }

            // 2. Metni pencereli parçalara (chunks) ayır
            var rawChunks = chunker.Chunk(pages, maxChunkSize: 800, overlap: 150);
            if (rawChunks.Count == 0)
            {
                return Results.BadRequest(new { error = "Metin parçalara ayrılamadı." });
            }

            // 3. Gemini ile tüm parçaları toplu (batch) vektörleştir
            var chunkTexts = rawChunks.Select(c => c.Content).ToList();
            var embeddings = await embeddingService.GenerateEmbeddingsBatchAsync(chunkTexts);

            // 4. Veritabanı Entity'lerini hazırla
            var document = new Document
            {
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                CreatedAt = DateTime.UtcNow
            };

            var chunkEntities = new List<DocumentChunk>(rawChunks.Count);
            for (int i = 0; i < rawChunks.Count; i++)
            {
                chunkEntities.Add(new DocumentChunk
                {
                    DocumentId = document.Id,
                    ChunkIndex = rawChunks[i].ChunkIndex,
                    PageNumber = rawChunks[i].PageNumber,
                    Content = rawChunks[i].Content,
                    Embedding = i < embeddings.Count ? embeddings[i] : null,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // 5. PostgreSQL + Pgvector'e kaydet
            await vectorStore.SaveDocumentWithChunksAsync(document, chunkEntities);

            var result = new DocumentUploadResultDto(
                DocumentId: document.Id,
                FileName: document.FileName,
                TotalPages: pages.Count,
                TotalChunks: chunkEntities.Count,
                UploadedAt: document.CreatedAt
            );

            return Results.Created($"/api/documents/{document.Id}", result);
        })
        .DisableAntiforgery()
        .WithName("UploadDocument")
        .WithSummary("Kurumsal doküman yükleme (PDF/Markdown)")
        .WithDescription("PDF veya Markdown dosyalarını sisteme yükler, metni sayfalara ve parçalara (chunk) ayırır, Gemini ile vektörleştirip Pgvector'e indeksler.");

        return group;
    }
}
