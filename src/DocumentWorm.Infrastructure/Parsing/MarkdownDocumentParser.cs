using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentWorm.Core.DTOs;
using DocumentWorm.Core.Interfaces;

namespace DocumentWorm.Infrastructure.Parsing;

public class MarkdownDocumentParser : IDocumentParser
{
    public bool CanParse(string contentType, string fileName)
    {
        return contentType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<ParsedPage>> ParseAsync(
        Stream stream, 
        string contentType, 
        string fileName, 
        CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var content = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<ParsedPage>();

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var pages = new List<ParsedPage>();
        var currentSection = new StringBuilder();
        int pageIndex = 1;

        foreach (var line in lines)
        {
            if (line.StartsWith("# ") && currentSection.Length > 200)
            {
                pages.Add(new ParsedPage(pageIndex++, currentSection.ToString().Trim()));
                currentSection.Clear();
            }
            currentSection.AppendLine(line);
        }

        if (currentSection.Length > 0)
        {
            pages.Add(new ParsedPage(pageIndex, currentSection.ToString().Trim()));
        }

        return pages;
    }
}
