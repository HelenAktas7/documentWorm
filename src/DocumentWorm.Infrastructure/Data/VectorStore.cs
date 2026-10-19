using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentWorm.Core.DTOs;
using DocumentWorm.Core.Entities;
using DocumentWorm.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace DocumentWorm.Infrastructure.Data;

public class VectorStore : IVectorStore
{
    private readonly DocumentWormDbContext _dbContext;

    public VectorStore(DocumentWormDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Dokümanı ve tüm parçalarını tek transaction'da kaydeder
    public async Task<Document> SaveDocumentWithChunksAsync(
        Document document, 
        IReadOnlyList<DocumentChunk> chunks, 
        CancellationToken ct = default)
    {
        await _dbContext.Documents.AddAsync(document, ct);
        await _dbContext.DocumentChunks.AddRangeAsync(chunks, ct);
        await _dbContext.SaveChangesAsync(ct);
        return document;
    }

    // Kullanıcının soru vektörüne en yakın Top-K parçayı çeker
    public async Task<IReadOnlyList<DocumentChunkMatch>> SearchSimilarAsync(
        Vector queryVector,
        int topK = 4,
        double minSimilarity = 0.5,
        CancellationToken ct = default)
    {
        // CosineDistance: 0 (birebir aynı) ile 2 (tam zıt) arasında bir mesafe döner.
        // Benzerlik Oranı (Cosine Similarity) = 1.0 - CosineDistance
        var matches = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Include(c => c.Document) // Hangi dokümandan geldiğini bilmek için
            .Where(c => c.Embedding != null)
            .Select(c => new
            {
                Chunk = c,
                Distance = c.Embedding!.CosineDistance(queryVector)
            })
            .OrderBy(x => x.Distance) // Mesafesi en küçük olan en yakındır
            .Take(topK * 2)           // Eşik kontrolü için biraz fazla aday çekiyoruz
            .ToListAsync(ct);

        var results = new List<DocumentChunkMatch>();
        foreach (var m in matches)
        {
            var similarity = 1.0 - m.Distance;
            // Belirlediğimiz benzerlik eşiğini (örneğin %50) geçenleri alıyoruz
            if (similarity >= minSimilarity)
            {
                results.Add(new DocumentChunkMatch(m.Chunk, similarity));
                if (results.Count >= topK)
                    break;
            }
        }

        return results;
    }
}
