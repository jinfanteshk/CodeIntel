namespace CodeIntel.Core;

public record RepoRequest(string Owner, string Repo, string Branch = "main", string Path = "");

public record CodeClass(string Id, string Name, string Namespace, string FilePath);
public record CodeMethod(string Id, string Name, string ClassId, string FilePath, string? Body);

public enum EdgeType
{
    DependsOn,
    Calls,
    Inherits,
    Implements
}

public record CodeEdge(string FromId, string ToId, EdgeType Type);

public record GraphModel(
    IReadOnlyList<CodeClass> Classes,
    IReadOnlyList<CodeMethod> Methods,
    IReadOnlyList<CodeEdge> Edges);

public record VectorDocument(
    string Id,
    string Content,
    float[] Embedding,
    string Type,
    string ClassName,
    string FilePath);
