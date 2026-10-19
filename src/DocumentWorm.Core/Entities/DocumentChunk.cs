using System;
using Pgvector;

namespace DocumentWorm.Core.Entities;

public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }

    public int ChunkIndex { get; set; }
    public int PageNumber { get; set; }
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gemini text-embedding-004 tarafından üretilen 768 boyutlu vektör koordinatı
    /// </summary>
    public Vector? Embedding { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
