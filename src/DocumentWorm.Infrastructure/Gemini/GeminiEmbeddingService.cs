using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentWorm.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;

namespace DocumentWorm.Infrastructure.Gemini;

public class GeminiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiEmbeddingService> _logger;

    public GeminiEmbeddingService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new Vector(new float[768]);

        var model = string.IsNullOrWhiteSpace(_options.EmbeddingModel) ? "gemini-embedding-001" : _options.EmbeddingModel;
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:embedContent?key={_options.ApiKey}";

        var requestBody = new
        {
            model = $"models/{model}",
            content = new
            {
                parts = new[] { new { text = text.Trim() } }
            },
            outputDimensionality = 768 // Veritabanımızla birebir uyumlu 768 boyut
        };

        var response = await _httpClient.PostAsJsonAsync(url, requestBody, ct);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning("Gemini Embedding kotası aşıldı, 5 saniye beklenip tekrar deneniyor...");
            await Task.Delay(5000, ct);
            return await GenerateEmbeddingAsync(text, ct);
        }

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Gemini Embedding API Hatası: {Status} - {Error}", response.StatusCode, err);
            throw new HttpRequestException($"Gemini Embedding API çağrısı başarısız ({response.StatusCode}): {err}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var values = json.GetProperty("embedding").GetProperty("values");

        var embedding = new float[values.GetArrayLength()];
        int i = 0;
        foreach (var val in values.EnumerateArray())
        {
            embedding[i++] = val.GetSingle();
        }

        return new Vector(embedding);
    }

    public async Task<IReadOnlyList<Vector>> GenerateEmbeddingsBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        if (texts == null || texts.Count == 0)
            return Array.Empty<Vector>();

        var model = string.IsNullOrWhiteSpace(_options.EmbeddingModel) ? "gemini-embedding-001" : _options.EmbeddingModel;
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:batchEmbedContents?key={_options.ApiKey}";

        var allEmbeddings = new List<Vector>();
        const int batchSize = 15;

        for (int i = 0; i < texts.Count; i += batchSize)
        {
            var currentChunk = texts.Skip(i).Take(batchSize).ToList();

            var requests = currentChunk.Select(t => new
            {
                model = $"models/{model}",
                content = new
                {
                    parts = new[] { new { text = string.IsNullOrWhiteSpace(t) ? " " : t.Trim() } }
                },
                outputDimensionality = 768 // Veritabanımızla birebir uyumlu 768 boyut
            }).ToArray();

            var requestBody = new { requests };

            var response = await _httpClient.PostAsJsonAsync(url, requestBody, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Gemini API kotası aşıldı (429). 10 saniye bekleniyor ve paket tekrar deneniyor...");
                await Task.Delay(10000, ct);
                i -= batchSize;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Gemini Batch Embedding Hatası ({Status}): {Error}", response.StatusCode, err);
                throw new HttpRequestException($"Gemini Batch Embedding çağrısı başarısız ({response.StatusCode}): {err}");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var embeddingsArray = json.GetProperty("embeddings");

            foreach (var embElement in embeddingsArray.EnumerateArray())
            {
                var values = embElement.GetProperty("values");
                var embedding = new float[values.GetArrayLength()];
                int vi = 0;
                foreach (var val in values.EnumerateArray())
                {
                    embedding[vi++] = val.GetSingle();
                }
                allEmbeddings.Add(new Vector(embedding));
            }

            if (i + batchSize < texts.Count)
            {
                await Task.Delay(300, ct);
            }
        }

        return allEmbeddings;
    }
}
