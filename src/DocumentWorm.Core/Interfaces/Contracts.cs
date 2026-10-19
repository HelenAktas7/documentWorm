using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocumentWorm.Core.DTOs;
using DocumentWorm.Core.Entities;
using Pgvector;

namespace DocumentWorm.Core.Interfaces;

public interface IDocumentParser
{
    bool CanParse(string contentType, string fileName);
    Task<IReadOnlyList<ParsedPage>> ParseAsync(Stream stream, string contentType, string fileName, CancellationToken ct = default);
}

public interface ITextChunker
{
    IReadOnlyList<RawChunk> Chunk(IReadOnlyList<ParsedPage> pages, int maxChunkSize = 800, int overlap = 150);
}

public interface IEmbeddingService
{
    Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<Vector>> GenerateEmbeddingsBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}

public interface IVectorStore
{
    Task<Document> SaveDocumentWithChunksAsync(Document document, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentChunkMatch>> SearchSimilarAsync(Vector queryVector, int topK = 4, double minSimilarity = 0.5, CancellationToken ct = default);
}

public interface IRagService
{
    Task<ChatQueryResponseDto> AskAsync(ChatQueryRequestDto request, CancellationToken ct = default);
}

public interface IApiKeyValidator
{
    Task<ApiClient?> ValidateAsync(string apiKey, CancellationToken ct = default);
    Task<string> GenerateApiKeyAsync(string clientName, int rateLimitPerMinute = 10, CancellationToken ct = default);
}
