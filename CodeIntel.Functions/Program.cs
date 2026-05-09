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
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(c =>
    {
        c.AddJsonFile("appsettings.json", optional: true);
        c.AddEnvironmentVariables();
    })
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureServices((ctx, services) =>
    {
        var cfg = ctx.Configuration;

        // GitHub (REAL - needs token)
        var ghToken = cfg["GitHub:Token"] ?? throw new InvalidOperationException("GitHub:Token missing");
        services.AddSingleton<IGitHubSource>(_ => new OctokitGitHubSource(ghToken));

        // Roslyn analyzer (REAL)
        services.AddSingleton<ICodeAnalyzer, RoslynAnalyzer>();

        // Neo4j Versioned Graph Store (ONLY option for production)
        var graphStoreType = cfg["GraphStore:Type"] ?? "Neo4jVersioned"; // "Neo4jVersioned" or "Mock"

        if (graphStoreType == "Neo4jVersioned")
        {
            // Neo4j with versioning support (PRODUCTION)
            var neo4jUri = cfg["Neo4j:Uri"] ?? "bolt://localhost:7687";
            var neo4jUser = cfg["Neo4j:User"] ?? "neo4j";
            var neo4jPassword = cfg["Neo4j:Password"]!;

            services.AddSingleton<IVersionedGraphStore>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<Neo4jVersionedGraphStore>>();
                return new Neo4jVersionedGraphStore(neo4jUri, neo4jUser, neo4jPassword, logger);
            });

            services.AddSingleton<IGraphStore>(sp => sp.GetRequiredService<IVersionedGraphStore>());

            services.AddSingleton<IVectorIndex>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<Neo4jVectorIndex>>();
                return new Neo4jVectorIndex(neo4jUri, neo4jUser, neo4jPassword, logger, 1536);
            });
        }
        else // Mock
        {
            services.AddSingleton<IGraphStore, MockGraphStore>();
            services.AddSingleton<IVectorIndex, MockVectorIndex>();
        }

        // Embedding Service (Azure OpenAI o Mock)
        bool useRealAzureOpenAI = !string.IsNullOrEmpty(cfg["AzureOpenAI:Endpoint"]);

        if (useRealAzureOpenAI)
        {
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
        }
        else
        {
            services.AddSingleton<IEmbeddingService, MockEmbeddingService>();
        }

        // ✅ NOTA: Vector Index ya está configurado arriba en cada GraphStore
        // No necesitamos Azure Search porque Neo4j maneja vectores nativamente

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
        Console.WriteLine($"📁 Downloaded to: {local}");

        var graphModel = await _analyzer.AnalyzeAsync(local, ct);
        Console.WriteLine($"📊 Analyzed: {graphModel.Classes.Count} classes, {graphModel.Methods.Count} methods, {graphModel.AspxPages.Count} ASPX pages, {graphModel.AspxControls.Count} controls, {graphModel.Edges.Count} edges");

        await _graph.UpsertAsync(req, graphModel, ct);

        await _index.EnsureIndexAsync(ct);

        var toIndex = new List<VectorDocument>();
        var chunks = CodeChunker.ToVectorDocs(graphModel).ToList();
        Console.WriteLine($"📦 Generated {chunks.Count} code chunks for embedding");

        foreach (var (id, content, type, className, filePath) in chunks)
        {
            ct.ThrowIfCancellationRequested();
            var emb = await _embed.EmbedAsync(content, ct);
            toIndex.Add(new VectorDocument(id, content, emb, type, className, filePath));
        }

        Console.WriteLine($"🔢 Generated {toIndex.Count} embeddings");

        await _index.UpsertAsync(toIndex, ct);

        return new
        {
            repo = $"{req.Owner}/{req.Repo}@{req.Branch}",
            downloadedTo = local,
            classes = graphModel.Classes.Count,
            methods = graphModel.Methods.Count,
            aspxPages = graphModel.AspxPages.Count,
            aspxControls = graphModel.AspxControls.Count,
            aspxEvents = graphModel.AspxEvents.Count,
            edges = graphModel.Edges.Count,
            chunksGenerated = chunks.Count,
            indexed = toIndex.Count
        };
    }
}