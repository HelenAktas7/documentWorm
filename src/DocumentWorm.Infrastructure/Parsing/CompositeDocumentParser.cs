using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentWorm.Core.DTOs;
using DocumentWorm.Core.Interfaces;

namespace DocumentWorm.Infrastructure.Parsing;

public class CompositeDocumentParser : IDocumentParser
{
    private readonly IEnumerable<IDocumentParser> _parsers;

    public CompositeDocumentParser(IEnumerable<IDocumentParser> parsers)
    {
        _parsers = parsers;
    }

    public bool CanParse(string contentType, string fileName)
    {
        return _parsers.Any(p => p.CanParse(contentType, fileName));
    }

    public async Task<IReadOnlyList<ParsedPage>> ParseAsync(
        Stream stream, 
        string contentType, 
        string fileName, 
        CancellationToken ct = default)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(contentType, fileName));
        if (parser == null)
        {
            throw new NotSupportedException($"Desteklenmeyen dosya türü: '{fileName}' ({contentType}). Desteklenenler: PDF, Markdown (.md), Plain text (.txt).");
        }

        return await parser.ParseAsync(stream, contentType, fileName, ct);
    }
}
