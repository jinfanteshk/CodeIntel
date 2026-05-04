using CodeIntel.Core;

namespace CodeIntel.Functions.Mocks;

public class MockEmbeddingService : IEmbeddingService
{
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        Console.WriteLine($"[MOCK] Creating embedding for: {text.Substring(0, Math.Min(50, text.Length))}...");
        // Return dummy 1536-dimensional vector
        var vec = new float[1536];
        for (int i = 0; i < vec.Length; i++)
            vec[i] = (float)new Random(text.GetHashCode() + i).NextDouble();
        await Task.Delay(50, ct);
        return vec;
    }
}