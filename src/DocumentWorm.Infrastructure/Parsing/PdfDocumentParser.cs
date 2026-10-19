using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocumentWorm.Core.DTOs;
using DocumentWorm.Core.Interfaces;
using UglyToad.PdfPig;

namespace DocumentWorm.Infrastructure.Parsing;

public class PdfDocumentParser : IDocumentParser
{
    public bool CanParse(string contentType, string fileName)
    {
        return contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<ParsedPage>> ParseAsync(
        Stream stream, 
        string contentType, 
        string fileName, 
        CancellationToken ct = default)
    {
        var pages = new List<ParsedPage>();

        using var pdfDocument = PdfDocument.Open(stream);
        foreach (var page in pdfDocument.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            var text = page.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                pages.Add(new ParsedPage(page.Number, text));
            }
        }

        return Task.FromResult<IReadOnlyList<ParsedPage>>(pages);
    }
}
