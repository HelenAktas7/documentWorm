namespace DocumentWorm.Infrastructure.Gemini;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";
    public string ChatModel { get; set; } = "gemini-3.8-flash";
}
