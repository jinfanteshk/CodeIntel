using CodeIntel.Core;

namespace CodeIntel.Functions.Mocks;

public class MockGraphStore : IGraphStore
{
    public async Task UpsertAsync(RepoRequest req, GraphModel model, CancellationToken ct)
    {
        Console.WriteLine($"[MOCK] Storing graph: {model.Classes.Count} classes, {model.Methods.Count} methods");
        await Task.Delay(100, ct);
    }
}