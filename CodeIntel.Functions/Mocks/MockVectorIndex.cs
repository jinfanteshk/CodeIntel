using CodeIntel.Core;

namespace CodeIntel.Functions.Mocks;

public class MockVectorIndex : IVectorIndex
{
    private List<VectorDocument> _docs = new();

    public async Task EnsureIndexAsync(CancellationToken ct)
    {
        Console.WriteLine("[MOCK] Ensuring vector index exists");
        await Task.Delay(50, ct);
    }

    public async Task UpsertAsync(IEnumerable<VectorDocument> docs, CancellationToken ct)
    {
        _docs.AddRange(docs);
        Console.WriteLine($"[MOCK] Indexed {docs.Count()} documents (total: {_docs.Count})");
        await Task.Delay(100, ct);
    }
}