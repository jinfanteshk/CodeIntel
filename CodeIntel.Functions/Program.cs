using CodeIntel.Core;
using CodeIntel.Functions.Mocks;
using CodeIntel.Graph;
using CodeIntel.Ingest.Chunking;
using CodeIntel.Ingest.GitHub;
using CodeIntel.Ingest.Roslyn;
using CodeIntel.Vector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(c =>
    {
        c.AddJsonFile("appsettings.json", optional: true);
        c.AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        var cfg = ctx.Configuration;

        // GitHub (REAL - needs token)
        var ghToken = cfg["GitHub:Token"] ?? throw new InvalidOperationException("GitHub:Token missing");
        services.AddSingleton<IGitHubSource>(_ => new OctokitGitHubSource(ghToken));

        // Roslyn analyzer (REAL)
        services.AddSingleton<ICodeAnalyzer, RoslynAnalyzer>();

        // Check if we're running locally without Azure services
        bool useRealAzure = !string.IsNullOrEmpty(cfg["CosmosGremlin:Host"]);

        if (useRealAzure)
        {
            // Real Azure services
            services.AddSingleton<IGraphStore>(_ => new CosmosGremlinGraphStore(
                host: cfg["CosmosGremlin:Host"]!,
                port: int.Parse(cfg["CosmosGremlin:Port"] ?? "443"),
                database: cfg["CosmosGremlin:Database"]!,
                graph: cfg["CosmosGremlin:Graph"]!,
                key: cfg["CosmosGremlin:Key"]!
            ));

            services.AddHttpClient();
            services.AddSingleton<IEmbeddingService>(sp =>
            {
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                return new AzureOpenAIEmbeddingService(
                    http,
                    cfg["AzureOpenAI:Endpoint"]!,
                    cfg["AzureOpenAI:ApiKey"]!,
                    cfg["AzureOpenAI:EmbeddingDeployment"]!,
                    cfg["AzureOpenAI:ApiVersion"] ?? "2024-06-01"
                );
            });

            services.AddSingleton<IVectorIndex>(_ => new AzureSearchVectorIndex(
                endpoint: cfg["Search:Endpoint"]!,
                apiKey: cfg["Search:ApiKey"]!,
                indexName: cfg["Search:IndexName"] ?? "codeintel",
                vectorDimensions: int.Parse(cfg["Search:VectorDimensions"] ?? "1536")
            ));
        }
        else
        {
            // Mock Azure services (for local development)
            Console.WriteLine("⚠️  Running with MOCK Azure services (local development mode)");
            services.AddSingleton<IGraphStore, MockGraphStore>();
            services.AddSingleton<IEmbeddingService, MockEmbeddingService>();
            services.AddSingleton<IVectorIndex, MockVectorIndex>();
        }

        // Orchestrator
        services.AddSingleton<IngestOrchestrator>();
    })
    .Build();

host.Run();

public sealed class IngestOrchestrator
{
    private readonly IGitHubSource _source;
    private readonly ICodeAnalyzer _analyzer;
    private readonly IGraphStore _graph;
    private readonly IEmbeddingService _embed;
    private readonly IVectorIndex _index;

    public IngestOrchestrator(IGitHubSource source, ICodeAnalyzer analyzer, IGraphStore graph, IEmbeddingService embed, IVectorIndex index)
    {
        _source = source;
        _analyzer = analyzer;
        _graph = graph;
        _embed = embed;
        _index = index;
    }

    public async Task<object> RunAsync(RepoRequest req, CancellationToken ct)
    {
        var local = await _source.DownloadRepositoryAsync(req, ct);
        var graphModel = await _analyzer.AnalyzeAsync(local, ct);
        await _graph.UpsertAsync(req, graphModel, ct);

        await _index.EnsureIndexAsync(ct);

        var toIndex = new List<VectorDocument>();
        foreach (var (id, content, type, className, filePath) in CodeChunker.ToVectorDocs(graphModel))
        {
            ct.ThrowIfCancellationRequested();
            var emb = await _embed.EmbedAsync(content, ct);
            toIndex.Add(new VectorDocument(id, content, emb, type, className, filePath));
        }

        await _index.UpsertAsync(toIndex, ct);

        return new
        {
            repo = $"{req.Owner}/{req.Repo}@{req.Branch}",
            downloadedTo = local,
            classes = graphModel.Classes.Count,
            methods = graphModel.Methods.Count,
            edges = graphModel.Edges.Count,
            indexed = toIndex.Count
        };
    }
}