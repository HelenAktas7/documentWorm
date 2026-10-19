using System;
using DocumentWorm.Core.Interfaces;
using DocumentWorm.Infrastructure.Auth;
using DocumentWorm.Infrastructure.Data;
using DocumentWorm.Infrastructure.Gemini;
using DocumentWorm.Infrastructure.Parsing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentWorm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // 1. PostgreSQL + Pgvector Bağlantısı
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=documentworm_db;Username=postgres;Password=postgres";

        services.AddDbContext<DocumentWormDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseVector(); // Pgvector uzantısını aktifleştir
                npgsqlOptions.MigrationsAssembly(typeof(DocumentWormDbContext).Assembly.FullName);
            });
        });

        // 2. Vektör Deposu (Vector Store)
        services.AddScoped<IVectorStore, VectorStore>();

         // 3. Doküman Okuyucuları (Composite Pattern - Döngüsel Bağımlılığı Önleme)
        services.AddSingleton<PdfDocumentParser>();
        services.AddSingleton<MarkdownDocumentParser>();
        services.AddSingleton<IDocumentParser>(sp => new CompositeDocumentParser(new IDocumentParser[]
        {
            sp.GetRequiredService<PdfDocumentParser>(),
            sp.GetRequiredService<MarkdownDocumentParser>()
        }));


        // 4. Metin Parçalayıcı (Chunker)
        services.AddSingleton<ITextChunker, SlidingWindowTextChunker>();

        // 5. Gemini Ayarları ve HttpClient Yapılandırması
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));

        services.AddHttpClient<IEmbeddingService, GeminiEmbeddingService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IRagService, RagService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // 6. API Key Doğrulayıcı
        services.AddScoped<IApiKeyValidator, ApiKeyValidator>();

        return services;
    }
}
