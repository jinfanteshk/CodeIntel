namespace CodeIntel.Core;

public interface IGitHubSource
{
    Task<string> DownloadRepositoryAsync(RepoRequest req, CancellationToken ct);
}

public interface ICodeAnalyzer
{
    Task<GraphModel> AnalyzeAsync(string localPath, CancellationToken ct);
}

public interface IGraphStore
{
    Task UpsertAsync(RepoRequest req, GraphModel model, CancellationToken ct);
}

public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}

public interface IVectorIndex
{
    Task EnsureIndexAsync(CancellationToken ct);
    Task UpsertAsync(IEnumerable<VectorDocument> docs, CancellationToken ct);
}
