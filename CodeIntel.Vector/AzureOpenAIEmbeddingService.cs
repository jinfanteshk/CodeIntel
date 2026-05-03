using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CodeIntel.Core;

namespace CodeIntel.Vector;

public sealed class AzureOpenAIEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _deployment;
    private readonly string _apiVersion;

    public AzureOpenAIEmbeddingService(HttpClient http, string endpoint, string apiKey, string deployment, string apiVersion)
    {
        _http = http;
        _endpoint = endpoint.TrimEnd('/');
        _apiKey = apiKey;
        _deployment = deployment;
        _apiVersion = apiVersion;

        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("api-key", _apiKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var url = $"{_endpoint}/openai/deployments/{_deployment}/embeddings?api-version={_apiVersion}";
        var payload = JsonSerializer.Serialize(new { input = text });
        var resp = await _http.PostAsync(url, new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var arr = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
        var vec = new float[arr.GetArrayLength()];
        int i = 0;
        foreach (var el in arr.EnumerateArray())
        {
            vec[i++] = el.GetSingle();
        }
        return vec;
    }
}
