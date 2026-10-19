using System;
using System.Collections.Generic;
using DocumentWorm.Core.Entities;

namespace DocumentWorm.Core.DTOs;

// Doküman okuyucudan çıkan sayfa bilgisi
public record ParsedPage(int PageNumber, string Text);

// Chunker'dan çıkan ham parça bilgisi (henüz vektörü yok)
public record RawChunk(int PageNumber, int ChunkIndex, string Content);

// Doküman yüklendiğinde kullanıcıya döneceğimiz özet bilgi
public record DocumentUploadResultDto(
    Guid DocumentId,
    string FileName,
    int TotalPages,
    int TotalChunks,
    DateTime UploadedAt);

// Kullanıcıya cevabın altında göstereceğimiz kaynak / atıf (Citation)
public record CitationDto(
    string DocumentName,
    int PageNumber,
    string RelevantSnippet,
    double RelevanceScore);

// Kullanıcının soru sorarken göndereceği istek gövdesi
public record ChatQueryRequestDto(
    string Question,
    int? TopK = 4,
    double? MinSimilarity = 0.5);

// Kullanıcıya döneceğimiz doğrulanmış yanıt ve kaynaklar
public record ChatQueryResponseDto(
    string Answer,
    IReadOnlyList<CitationDto> Citations,
    long LatencyMs,
    bool FoundContext);

// Pgvector benzerlik aramasından dönen eşleşme
public record DocumentChunkMatch(
    DocumentChunk Chunk,
    double CosineSimilarity);
