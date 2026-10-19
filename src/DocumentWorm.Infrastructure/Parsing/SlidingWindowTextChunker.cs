using System;
using System.Collections.Generic;
using DocumentWorm.Core.DTOs;
using DocumentWorm.Core.Interfaces;

namespace DocumentWorm.Infrastructure.Parsing;

public class SlidingWindowTextChunker : ITextChunker
{
    public IReadOnlyList<RawChunk> Chunk(
        IReadOnlyList<ParsedPage> pages, 
        int maxChunkSize = 800, 
        int overlap = 150)
    {
        var chunks = new List<RawChunk>();
        int globalChunkIndex = 0;

        foreach (var page in pages)
        {
            var text = page.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            // Metin zaten küçükse doğrudan tek parça olarak ekle
            if (text.Length <= maxChunkSize)
            {
                chunks.Add(new RawChunk(page.PageNumber, globalChunkIndex++, text));
                continue;
            }

            int startIndex = 0;
            while (startIndex < text.Length)
            {
                int remaining = text.Length - startIndex;
                int currentChunkSize = Math.Min(maxChunkSize, remaining);

                // Eğer metnin sonuna gelmediysek, cümlenin bittiği doğal noktayı ara (. ? ! \n)
                if (startIndex + currentChunkSize < text.Length)
                {
                    int searchStart = Math.Max(0, currentChunkSize - 80);
                    int breakIndex = -1;

                    for (int i = currentChunkSize - 1; i >= searchStart; i--)
                    {
                        char c = text[startIndex + i];
                        if (c == '.' || c == '\n' || c == '?' || c == '!' || c == ';')
                        {
                            breakIndex = i + 1;
                            break;
                        }
                    }

                    // Cümle bitişi bulamadıysa en azından kelimeyi bölmemek için boşluk ara
                    if (breakIndex == -1)
                    {
                        for (int i = currentChunkSize - 1; i >= searchStart; i--)
                        {
                            if (char.IsWhiteSpace(text[startIndex + i]))
                            {
                                breakIndex = i + 1;
                                break;
                            }
                        }
                    }

                    if (breakIndex > 0)
                    {
                        currentChunkSize = breakIndex;
                    }
                }

                var chunkContent = text.Substring(startIndex, currentChunkSize).Trim();
                if (chunkContent.Length > 20) // Çok kısa anlamsız kalıntıları atla
                {
                    chunks.Add(new RawChunk(page.PageNumber, globalChunkIndex++, chunkContent));
                }

                // Bir sonraki pencereye (chunkSize - overlap) kadar ilerle
                int step = Math.Max(1, currentChunkSize - overlap);
                startIndex += step;
            }
        }

        return chunks;
    }
}
