using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using CodeIntel.Core;

namespace CodeIntel.Vector;

public sealed class AzureSearchVectorIndex : IVectorIndex
{
    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly string _indexName;
    private readonly int _dims;

    public AzureSearchVectorIndex(string endpoint, string apiKey, string indexName, int vectorDimensions)
    {
        _indexName = indexName;
        _dims = vectorDimensions;

        var credential = new AzureKeyCredential(apiKey);
        _indexClient = new SearchIndexClient(new Uri(endpoint), credential);
        _searchClient = new SearchClient(new Uri(endpoint), indexName, credential);
    }

    public async Task EnsureIndexAsync(CancellationToken ct)
    {
        try
        {
            await _indexClient.GetIndexAsync(_indexName, ct);
            return;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // crear
        }

        var fields = new List<SearchField>
        {
            new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
            new SearchableField("content") { IsFilterable = false },
            new SimpleField("type", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("className", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("filePath", SearchFieldDataType.String) { IsFilterable = true },
            new SearchField("embedding", SearchFieldDataType.Collection(SearchFieldDataType.Single))
            {
                IsSearchable = true,
                VectorSearchDimensions = _dims,
                VectorSearchProfileName = "v1"
            }
        };

        var index = new SearchIndex(_indexName)
        {
            Fields = fields,
            VectorSearch = new VectorSearch
            {
                Profiles = { new VectorSearchProfile("v1", "hnsw") },
                Algorithms = { new HnswAlgorithmConfiguration("hnsw") }
            }
        };

        await _indexClient.CreateIndexAsync(index, ct);
    }

    public async Task UpsertAsync(IEnumerable<VectorDocument> docs, CancellationToken ct)
    {
        var batch = IndexDocumentsBatch.Upload(docs.Select(d => new
        {
            id = d.Id,
            content = d.Content,
            type = d.Type,
            className = d.ClassName,
            filePath = d.FilePath,
            embedding = d.Embedding
        }));

        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: ct);
    }
}
