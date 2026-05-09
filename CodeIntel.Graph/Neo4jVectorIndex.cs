using CodeIntel.Core;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace CodeIntel.Graph;

public class Neo4jVectorIndex : IVectorIndex, IAsyncDisposable
{
    private readonly IDriver _driver;
    private readonly ILogger<Neo4jVectorIndex> _logger;
    private readonly int _vectorDimensions;
    private const string VectorIndexName = "code_embeddings";

    public Neo4jVectorIndex(string uri, string user, string password, ILogger<Neo4jVectorIndex> logger, int vectorDimensions = 1536)
    {
        // El driver detecta automáticamente TLS desde el esquema URI (neo4j+s://)
        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password), o =>
        {
            o.WithMaxConnectionLifetime(TimeSpan.FromMinutes(30));
            o.WithMaxConnectionPoolSize(50);
            o.WithConnectionTimeout(TimeSpan.FromMinutes(2));
        });
        _logger = logger;
        _vectorDimensions = vectorDimensions;
    }

    public async Task EnsureIndexAsync(CancellationToken ct)
    {
        _logger.LogInformation("Ensuring Neo4j vector index exists: {IndexName}", VectorIndexName);

        await using var session = _driver.AsyncSession();

        try
        {
            // Check if index exists
            var indexExists = await session.ExecuteReadAsync(async tx =>
            {
                var result = await tx.RunAsync(@"
                    SHOW INDEXES
                    YIELD name, type
                    WHERE name = $indexName AND type = 'VECTOR'
                    RETURN count(*) > 0 as exists
                ", new { indexName = VectorIndexName });

                var record = await result.SingleAsync();
                return record["exists"].As<bool>();
            });

            if (!indexExists)
            {
                _logger.LogInformation("Creating vector index {IndexName} with {Dimensions} dimensions", 
                    VectorIndexName, _vectorDimensions);

                await session.ExecuteWriteAsync(async tx =>
                {
                    // Create vector index on CodeNode.embedding
                    await tx.RunAsync($@"
                        CREATE VECTOR INDEX {VectorIndexName} IF NOT EXISTS
                        FOR (n:CodeNode)
                        ON (n.embedding)
                        OPTIONS {{
                            indexConfig: {{
                                `vector.dimensions`: $dimensions,
                                `vector.similarity_function`: 'cosine'
                            }}
                        }}
                    ", new { dimensions = _vectorDimensions });
                });

                _logger.LogInformation("Vector index {IndexName} created successfully", VectorIndexName);
            }
            else
            {
                _logger.LogInformation("Vector index {IndexName} already exists", VectorIndexName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure vector index {IndexName}", VectorIndexName);
            throw;
        }
    }

    public async Task UpsertAsync(IEnumerable<VectorDocument> docs, CancellationToken ct)
    {
        var docList = docs.ToList();
        _logger.LogInformation("Upserting {Count} vector documents to Neo4j", docList.Count);

        await using var session = _driver.AsyncSession();

        try
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                foreach (var doc in docList)
                {
                    ct.ThrowIfCancellationRequested();

                    // Create or update CodeNode with embedding
                    await tx.RunAsync(@"
                        MERGE (n:CodeNode {id: $id})
                        SET n.content = $content,
                            n.embedding = $embedding,
                            n.type = $type,
                            n.className = $className,
                            n.filePath = $filePath,
                            n.lastUpdated = datetime()

                        // Link to corresponding Class or Method node if they exist
                        WITH n
                        OPTIONAL MATCH (entity {id: $id})
                        WHERE entity:Class OR entity:Method
                        FOREACH (_ IN CASE WHEN entity IS NOT NULL THEN [1] ELSE [] END |
                            MERGE (n)-[:REPRESENTS]->(entity)
                        )
                    ",
                    new
                    {
                        id = doc.Id,
                        content = doc.Content,
                        embedding = doc.Embedding.ToArray(),
                        type = doc.Type,
                        className = doc.ClassName,
                        filePath = doc.FilePath
                    });
                }
            });

            _logger.LogInformation("Successfully upserted {Count} vector documents", docList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert vector documents");
            throw;
        }
    }

    /// <summary>
    /// Search for similar code using vector similarity
    /// </summary>
    public async Task<List<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int topK = 10, CancellationToken ct = default)
    {
        _logger.LogInformation("Searching for top {TopK} similar code nodes", topK);

        await using var session = _driver.AsyncSession();

        try
        {
            var results = await session.ExecuteReadAsync(async tx =>
            {
                var result = await tx.RunAsync($@"
                    CALL db.index.vector.queryNodes($indexName, $topK, $queryVector)
                    YIELD node, score
                    RETURN node.id as id,
                           node.content as content,
                           node.type as type,
                           node.className as className,
                           node.filePath as filePath,
                           score
                    ORDER BY score DESC
                ", new
                {
                    indexName = VectorIndexName,
                    topK,
                    queryVector = queryEmbedding
                });

                var searchResults = new List<VectorSearchResult>();
                await foreach (var record in result)
                {
                    searchResults.Add(new VectorSearchResult
                    {
                        Id = record["id"].As<string>(),
                        Content = record["content"].As<string>(),
                        Type = record["type"].As<string>(),
                        ClassName = record["className"].As<string>(),
                        FilePath = record["filePath"].As<string>(),
                        Score = record["score"].As<double>()
                    });
                }

                return searchResults;
            });

            _logger.LogInformation("Found {Count} similar results", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search vectors");
            throw;
        }
    }

    /// <summary>
    /// Hybrid search: Vector similarity + Graph traversal
    /// Example: "Find similar methods that are called by a specific class"
    /// </summary>
    public async Task<List<VectorSearchResult>> HybridSearchAsync(
        float[] queryEmbedding, 
        string? filterByClassId = null, 
        int topK = 10, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Hybrid search: similarity + graph filtering (classId: {ClassId})", filterByClassId);

        await using var session = _driver.AsyncSession();

        try
        {
            var results = await session.ExecuteReadAsync(async tx =>
            {
                var cypherQuery = filterByClassId != null
                    ? @"
                        CALL db.index.vector.queryNodes($indexName, $topK * 3, $queryVector)
                        YIELD node, score

                        // Filter: only methods belonging to the specified class
                        MATCH (node)-[:REPRESENTS]->(m:Method)-[:HAS_METHOD]-(c:Class {id: $classId})

                        RETURN node.id as id,
                               node.content as content,
                               node.type as type,
                               node.className as className,
                               node.filePath as filePath,
                               score
                        ORDER BY score DESC
                        LIMIT $topK
                    "
                    : @"
                        CALL db.index.vector.queryNodes($indexName, $topK, $queryVector)
                        YIELD node, score
                        RETURN node.id as id,
                               node.content as content,
                               node.type as type,
                               node.className as className,
                               node.filePath as filePath,
                               score
                        ORDER BY score DESC
                    ";

                var result = await tx.RunAsync(cypherQuery, new
                {
                    indexName = VectorIndexName,
                    topK,
                    queryVector = queryEmbedding,
                    classId = filterByClassId
                });

                var searchResults = new List<VectorSearchResult>();
                await foreach (var record in result)
                {
                    searchResults.Add(new VectorSearchResult
                    {
                        Id = record["id"].As<string>(),
                        Content = record["content"].As<string>(),
                        Type = record["type"].As<string>(),
                        ClassName = record["className"].As<string>(),
                        FilePath = record["filePath"].As<string>(),
                        Score = record["score"].As<double>()
                    });
                }

                return searchResults;
            });

            _logger.LogInformation("Hybrid search found {Count} results", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform hybrid search");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_driver != null)
        {
            await _driver.DisposeAsync();
        }
    }

    /// <summary>
    /// GraphRAG: Vector search + Graph traversal (1-2 hops)
    /// Returns semantic matches PLUS their related code entities (dependencies, callers, etc.)
    /// This is the CORE advantage of Neo4j as a Vector Graph Database
    /// </summary>
    public async Task<GraphRAGResult> GraphRAGSearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        int maxHops = 1,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GraphRAG search: top-{TopK} with {MaxHops}-hop traversal", topK, maxHops);

        await using var session = _driver.AsyncSession();

        try
        {
            var result = await session.ExecuteReadAsync(async tx =>
            {
                // Step 1: Vector search for top-k similar code chunks
                // Step 2: Traverse graph to find related entities (CALLS, DEPENDS_ON, HAS_METHOD, etc.)
                var cypherQuery = @"
                    // 1️⃣ Vector search: Find top-K similar code nodes
                    CALL db.index.vector.queryNodes($indexName, $topK, $queryVector)
                    YIELD node AS startNode, score

                    // 2️⃣ Graph traversal: Find related entities within maxHops
                    CALL {
                        WITH startNode
                        MATCH path = (startNode)-[:REPRESENTS*0..1]->(entity)
                        WHERE entity:Class OR entity:Method
                        OPTIONAL MATCH (entity)-[rel:CALLS|DEPENDS_ON|HAS_METHOD|INHERITS*1.." + maxHops + @"]-(related)
                        WHERE related:Class OR related:Method
                        RETURN entity, collect(DISTINCT related) as relatedEntities, collect(DISTINCT type(rel)) as relationshipTypes
                    }

                    // 3️⃣ Return enriched context
                    RETURN 
                        startNode.id as id,
                        startNode.content as content,
                        startNode.type as type,
                        startNode.className as className,
                        startNode.filePath as filePath,
                        score,
                        entity.id as entityId,
                        entity.name as entityName,
                        labels(entity) as entityLabels,
                        [r in relatedEntities | {id: r.id, name: r.name, type: labels(r)[0]}] as relatedNodes,
                        relationshipTypes
                    ORDER BY score DESC
                ";

                var queryResult = await tx.RunAsync(cypherQuery, new
                {
                    indexName = VectorIndexName,
                    topK,
                    queryVector = queryEmbedding
                });

                var matches = new List<GraphRAGMatch>();
                await foreach (var record in queryResult)
                {
                    var match = new GraphRAGMatch
                    {
                        // Vector match info
                        Id = record["id"].As<string>(),
                        Content = record["content"].As<string>(),
                        Type = record["type"].As<string>(),
                        ClassName = record["className"].As<string>(),
                        FilePath = record["filePath"].As<string>(),
                        Score = record["score"].As<double>(),

                        // Graph entity info
                        EntityId = record["entityId"].As<string>(),
                        EntityName = record["entityName"].As<string>(),
                        EntityType = record["entityLabels"].As<List<string>>().FirstOrDefault() ?? "Unknown",

                        // Related entities (context)
                        RelatedEntities = record["relatedNodes"].As<List<Dictionary<string, object>>>()
                            .Select(n => new RelatedEntity
                            {
                                Id = n["id"].ToString()!,
                                Name = n["name"].ToString()!,
                                Type = n["type"].ToString()!
                            }).ToList(),

                        RelationshipTypes = record["relationshipTypes"].As<List<string>>().Distinct().ToList()
                    };

                    matches.Add(match);
                }

                return new GraphRAGResult
                {
                    Matches = matches,
                    TotalMatches = matches.Count
                };
            });

            _logger.LogInformation("GraphRAG search returned {Count} enriched matches", result.TotalMatches);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform GraphRAG search");
            throw;
        }
    }
}

// Result model for search operations
public class VectorSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public double Score { get; set; }
}

// GraphRAG result models
public class GraphRAGResult
{
    public List<GraphRAGMatch> Matches { get; set; } = new();
    public int TotalMatches { get; set; }
}

public class GraphRAGMatch
{
    // Vector search result
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public double Score { get; set; }

    // Graph entity
    public string EntityId { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;

    // Graph context (relationships)
    public List<RelatedEntity> RelatedEntities { get; set; } = new();
    public List<string> RelationshipTypes { get; set; } = new();
}

public class RelatedEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
