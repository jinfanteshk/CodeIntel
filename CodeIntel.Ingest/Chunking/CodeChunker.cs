using CodeIntel.Core;
using System.Collections.Generic;
using System.Linq;

namespace CodeIntel.Ingest.Chunking;

public static class CodeChunker
{
    /// <summary>
    /// MVP: convierte métodos en documentos para embedding.
    /// Puedes mejorar con chunking por tokens y docstrings.
    /// </summary>
    public static IEnumerable<(string id, string content, string type, string className, string filePath)> ToVectorDocs(GraphModel model)
    {
        var classById = model.Classes
            .DistinctBy(c => c.Id)  // Mantiene solo la primera ocurrencia
            .ToDictionary(c => c.Id, c => c);

        foreach (var m in model.Methods)
        {
            if (!classById.TryGetValue(m.ClassId, out var cls)) continue;

            var content = @$"Namespace: {cls.Namespace}
Class: {cls.Name}
Method: {m.Name}
File: {m.FilePath}

{m.Body}";
            yield return (m.Id, content, "method", cls.Name, m.FilePath);
        }

        foreach (var c in model.Classes)
        {
            var content = @$"Namespace: {c.Namespace}
Class: {c.Name}
File: {c.FilePath}";
            yield return (c.Id, content, "class", c.Name, c.FilePath);
        }
    }
}
