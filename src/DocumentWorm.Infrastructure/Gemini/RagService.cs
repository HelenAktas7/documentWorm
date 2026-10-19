using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DocumentWorm.Core.DTOs;
using DocumentWorm.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentWorm.Infrastructure.Gemini;

public class RagService : IRagService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<RagService> _logger;

    public RagService(
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<RagService> logger)
    {
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    private string GetApiKey()
    {
        var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(key))
            return key;

        return _options.ApiKey;
    }

    public async Task<ChatQueryResponseDto> AskAsync(ChatQueryRequestDto request, CancellationToken ct = default)
    {
        // 1. Yanıt gecikmesini (latency) milisaniye cinsinden ölçmek için kronometre başlatıyoruz
        var stopwatch = Stopwatch.StartNew();

        int topK = request.TopK.GetValueOrDefault(4);
        double minSimilarity = request.MinSimilarity.GetValueOrDefault(0.45);

        // 2. RETRIEVAL (Arama): Soruyu vektöre dönüştür
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(request.Question, ct);

        // 3. Pgvector üzerinden en alakalı parçaları çek
        var matches = await _vectorStore.SearchSimilarAsync(queryVector, topK, minSimilarity, ct);

        // Eğer eşleşen parça yoksa veya eşik değerinin altındaysa: Modele hiç gitmeden HALÜSİNASYONU ÖNLE!
        if (matches == null || matches.Count == 0)
        {
            stopwatch.Stop();
            return new ChatQueryResponseDto(
                Answer: "Sorunuzla ilgili yüklenen dokümanlarda eşleşen bir bilgi bulunamadı.",
                Citations: Array.Empty<CitationDto>(),
                LatencyMs: stopwatch.ElapsedMilliseconds,
                FoundContext: false
            );
        }

        // 4. AUGMENTATION (Zenginleştirme): Kullanıcıya döneceğimiz Atıf (Citation) listesini hazırla
        var citations = matches.Select(m => new CitationDto(
            DocumentName: m.Chunk.Document?.FileName ?? "Bilinmeyen Doküman",
            PageNumber: m.Chunk.PageNumber,
            RelevantSnippet: m.Chunk.Content.Length > 200 ? m.Chunk.Content.Substring(0, 200) + "..." : m.Chunk.Content,
            RelevanceScore: Math.Round(m.CosineSimilarity, 4)
        )).ToList();

        // Gemini'ye verilecek bağlam metnini oluştur
        var contextBuilder = new StringBuilder();
        int snippetIndex = 1;
        foreach (var match in matches)
        {
            var docName = match.Chunk.Document?.FileName ?? "Doküman";
            contextBuilder.AppendLine($"--- [KAYNAK {snippetIndex}: {docName} | Sayfa {match.Chunk.PageNumber}] ---");
            contextBuilder.AppendLine(match.Chunk.Content);
            contextBuilder.AppendLine();
            snippetIndex++;
        }

        // 5. ANTI-HALÜSİNASYON SİSTEM İSTEMİ (Prompt Engineering)
        var systemPrompt = @"Sen kurumsal ve güvenilir bir dokümantasyon asistanısın (DocumentWorm).
Görevin, kullanıcının sorusunu YALNIZCA ve YALNIZCA aşağıda verilen [DOKÜMAN BAĞLAMI] içindeki bilgilere dayanarak yanıtlamaktır.

TEMEL GÜVENLİK VE HALÜSİNASYON KURALLARI:
1. Kesinlikle verilen bağlam dışındaki genel bilgilerden, varsayımlardan veya harici kaynaklardan bilgi ekleme.
2. Eğer sorulan sorunun yanıtı verilen bağlam parçalarında açık ve net biçimde yer almıyorsa, kesinlikle 'Yüklenen dokümanlarda bu bilgiye ulaşılamamıştır.' şeklinde yanıt ver.
3. Yanıtında ilgili bilgiyi hangi dokümandan ve sayfadan aldığını parantez içinde belirt (Örn: [Yonetmelik.pdf, Sayfa 4]).
4. Yanıtını anlaşılır, profesyonel, maddeli ve kurumsal bir Türkçe üslupla sun.";

        var userMessage = $@"Aşağıdaki doküman parçalarını kullanarak sorumu yanıtla:

[DOKÜMAN BAĞLAMI]
{contextBuilder}

SORU:
{request.Question}";

        // 6. GENERATION (Üretim): Gemini modelini çağır
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Gemini API Key bulunamadı!");
        }

        var model = string.IsNullOrWhiteSpace(_options.ChatModel) ? "gemini-2.5-flash" : _options.ChatModel;
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = systemPrompt + "\n\n" + userMessage }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1, // Düşük sıcaklık: Halüsinasyonu önler, modele yaratıcılık değil doğruluk katar
                maxOutputTokens = 1024
            }
        };

        var response = await _httpClient.PostAsJsonAsync(url, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Gemini GenerateContent API Hatası: {StatusCode} - {Error}", response.StatusCode, err);
            throw new HttpRequestException($"Gemini yanıt üretimi başarısız ({response.StatusCode}): {err}");
        }

        var chatResult = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>(cancellationToken: ct);
        var answerText = chatResult?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text?.Trim()
                         ?? "Cevap üretilemedi.";

        stopwatch.Stop();

        return new ChatQueryResponseDto(
            Answer: answerText,
            Citations: citations,
            LatencyMs: stopwatch.ElapsedMilliseconds,
            FoundContext: true
        );
    }

    private sealed class GeminiGenerateResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }
    }

    private sealed class Candidate
    {
        [JsonPropertyName("content")]
        public CandidateContent? Content { get; set; }
    }

    private sealed class CandidateContent
    {
        [JsonPropertyName("parts")]
        public List<CandidatePart>? Parts { get; set; }
    }

    private sealed class CandidatePart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
