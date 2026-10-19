using DocumentWorm.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace DocumentWorm.Infrastructure.Data;

public class DocumentWormDbContext : DbContext
{
    public DocumentWormDbContext(DbContextOptions<DocumentWormDbContext> options) : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PostgreSQL'de pgvector eklentisini aktif eder
        modelBuilder.HasPostgresExtension("vector");

        // Document tablosu ayarları
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);

            // Bir doküman silindiğinde tüm parçaları (chunks) da otomatik silinsin (Cascade)
            entity.HasMany(e => e.Chunks)
                  .WithOne(c => c.Document)
                  .HasForeignKey(c => c.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // DocumentChunk tablosu ve HNSW Vektör İndeksi
        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            
            // 768 boyutlu vektör sütunu tanımlıyoruz
            entity.Property(e => e.Embedding).HasColumnType("vector(768)");

            // Milisaniyelik semantik arama için HNSW Kosinüs İndeksi kuruyoruz
            entity.HasIndex(e => e.Embedding)
                  .HasMethod("hnsw")
                  .HasOperators("vector_cosine_ops");
        });

        // ApiClient tablosu
        modelBuilder.Entity<ApiClient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.ApiKeyHash).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ApiKeyPrefix).IsRequired().HasMaxLength(20);
            
            // Aynı API key hash'inden iki tane olamaz (Unique)
            entity.HasIndex(e => e.ApiKeyHash).IsUnique();
        });
    }
}
