using CodeIntel.Core;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace CodeIntel.Graph;

/// <summary>
/// Implementación de GraphStore con versionado temporal para rollback.
/// Mantiene historial completo de cambios y permite volver a cualquier punto en el tiempo.
/// </summary>
public class Neo4jVersionedGraphStore : IVersionedGraphStore, IAsyncDisposable
{
    private readonly IDriver _driver;
    private readonly ILogger<Neo4jVersionedGraphStore> _logger;

    public Neo4jVersionedGraphStore(string uri, string user, string password, ILogger<Neo4jVersionedGraphStore> logger)
    {
        // El driver detecta automáticamente TLS desde el esquema URI (neo4j+s://)
        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password), o =>
        {
            o.WithMaxConnectionLifetime(TimeSpan.FromMinutes(30));
            o.WithMaxConnectionPoolSize(50);
            o.WithConnectionTimeout(TimeSpan.FromMinutes(2));
        });
        _logger = logger;
    }

    public async Task UpsertAsync(RepoRequest req, GraphModel model, CancellationToken ct)
    {
        var repoId = $"{req.Owner}/{req.Repo}@{req.Branch}";
        var versionId = Guid.NewGuid().ToString(); // ID único para esta versión
        var timestamp = DateTimeOffset.UtcNow;
        var commitHash = req.Path; // Asumiendo que Path contiene el commit hash o PR number

        _logger.LogInformation("Storing versioned graph for {RepoId} - Version: {VersionId}, Commit: {CommitHash}",
            repoId, versionId, commitHash);

        await using var session = _driver.AsyncSession();

        try
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                // 1. Crear nodo de versión (snapshot)
                await tx.RunAsync(@"
                    MERGE (r:Repository {id: $repoId})
                    SET r.owner = $owner,
                        r.name = $repo,
                        r.branch = $branch,
                        r.currentVersion = $versionId,
                        r.lastUpdated = datetime()

                    CREATE (v:Version {
                        id: $versionId,
                        repoId: $repoId,
                        commitHash: $commitHash,
                        timestamp: $timestamp,
                        isCurrent: true
                    })

                    MERGE (r)-[:HAS_VERSION]->(v)

                    // Marcar versión anterior como no-current
                    WITH r
                    MATCH (r)-[:HAS_VERSION]->(oldV:Version)
                    WHERE oldV.isCurrent = true AND oldV.id <> $versionId
                    SET oldV.isCurrent = false
                ",
                new
                {
                    repoId,
                    owner = req.Owner,
                    repo = req.Repo,
                    branch = req.Branch,
                    versionId,
                    commitHash,
                    timestamp = timestamp.ToUnixTimeSeconds()
                });

                // 2. Cerrar versiones anteriores de clases (soft delete)
                await tx.RunAsync(@"
                    MATCH (c:Class {repoId: $repoId})
                    WHERE c.validTo IS NULL
                    SET c.validTo = $timestamp
                ",
                new { repoId, timestamp = timestamp.ToUnixTimeSeconds() });

                // 3. Crear nuevas versiones de clases
                foreach (var cls in model.Classes)
                {
                    await tx.RunAsync(@"
                        CREATE (c:Class {
                            id: $id,
                            versionId: $versionId,
                            name: $name,
                            namespace: $namespace,
                            filePath: $filePath,
                            repoId: $repoId,
                            validFrom: $timestamp,
                            validTo: null
                        })
                        WITH c
                        MATCH (v:Version {id: $versionId})
                        MERGE (v)-[:CONTAINS]->(c)

                        // Enlazar con versión anterior si existe
                        WITH c
                        OPTIONAL MATCH (prev:Class {repoId: $repoId})
                        WHERE prev.id = $id 
                          AND prev.validTo = $timestamp
                          AND prev.versionId <> $versionId
                        FOREACH (_ IN CASE WHEN prev IS NOT NULL THEN [1] ELSE [] END |
                            MERGE (prev)-[:NEXT_VERSION]->(c)
                        )
                    ",
                    new
                    {
                        id = cls.Id,
                        versionId,
                        name = cls.Name,
                        @namespace = cls.Namespace,
                        filePath = cls.FilePath,
                        repoId,
                        timestamp = timestamp.ToUnixTimeSeconds()
                    });
                }

                // 4. Cerrar versiones anteriores de métodos
                await tx.RunAsync(@"
                    MATCH (m:Method {repoId: $repoId})
                    WHERE m.validTo IS NULL
                    SET m.validTo = $timestamp
                ",
                new { repoId, timestamp = timestamp.ToUnixTimeSeconds() });

                // 5. Crear nuevas versiones de métodos
                foreach (var method in model.Methods)
                {
                    await tx.RunAsync(@"
                        CREATE (m:Method {
                            id: $id,
                            versionId: $versionId,
                            name: $name,
                            classId: $classId,
                            filePath: $filePath,
                            body: $body,
                            repoId: $repoId,
                            validFrom: $timestamp,
                            validTo: null
                        })
                        WITH m
                        MATCH (c:Class {id: $classId, versionId: $versionId})
                        MERGE (c)-[:HAS_METHOD]->(m)

                        // Enlazar con versión anterior
                        WITH m
                        OPTIONAL MATCH (prev:Method {repoId: $repoId})
                        WHERE prev.id = $id 
                          AND prev.validTo = $timestamp
                          AND prev.versionId <> $versionId
                        FOREACH (_ IN CASE WHEN prev IS NOT NULL THEN [1] ELSE [] END |
                            MERGE (prev)-[:NEXT_VERSION]->(m)
                        )
                    ",
                    new
                    {
                        id = method.Id,
                        versionId,
                        name = method.Name,
                        classId = method.ClassId,
                        filePath = method.FilePath,
                        body = method.Body,
                        repoId,
                        timestamp = timestamp.ToUnixTimeSeconds()
                    });
                }

                // 6. Crear relaciones versionadas
                foreach (var edge in model.Edges)
                {
                    var relType = edge.Type.ToString().ToUpper();

                    await tx.RunAsync($@"
                        MATCH (from {{id: $fromId, versionId: $versionId}})
                        MATCH (to {{id: $toId, versionId: $versionId}})
                        CREATE (from)-[r:{relType} {{
                            versionId: $versionId,
                            repoId: $repoId,
                            validFrom: $timestamp,
                            validTo: null
                        }}]->(to)
                    ",
                    new
                    {
                        fromId = edge.FromId,
                        toId = edge.ToId,
                        versionId,
                        repoId,
                        timestamp = timestamp.ToUnixTimeSeconds()
                    });
                }

                _logger.LogInformation("Successfully stored versioned graph for {RepoId} - Version: {VersionId}", 
                    repoId, versionId);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store versioned graph for {RepoId}", repoId);
            throw;
        }
    }

    /// <summary>
    /// Rollback a una versión específica
    /// </summary>
    public async Task RollbackToVersionAsync(string repoId, string versionId, CancellationToken ct)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            // Marcar la versión target como current
            await tx.RunAsync(@"
                MATCH (r:Repository {id: $repoId})
                MATCH (r)-[:HAS_VERSION]->(targetV:Version {id: $versionId})

                // Desmarcar versión actual
                OPTIONAL MATCH (r)-[:HAS_VERSION]->(currentV:Version {isCurrent: true})
                SET currentV.isCurrent = false

                // Marcar target como current
                SET targetV.isCurrent = true,
                    r.currentVersion = $versionId
            ",
            new { repoId, versionId });
        });

        _logger.LogInformation("Rolled back {RepoId} to version {VersionId}", repoId, versionId);
    }

    /// <summary>
    /// Obtener grafo en un punto específico del tiempo
    /// </summary>
    public async Task<GraphModel> GetGraphAtTimestampAsync(string repoId, long timestamp, CancellationToken ct)
    {
        await using var session = _driver.AsyncSession();

        var classes = new List<CodeClass>();
        var methods = new List<CodeMethod>();
        var edges = new List<CodeEdge>();

        await session.ExecuteReadAsync(async tx =>
        {
            // Obtener clases válidas en ese timestamp
            var classResult = await tx.RunAsync(@"
                MATCH (c:Class {repoId: $repoId})
                WHERE c.validFrom <= $timestamp 
                  AND (c.validTo IS NULL OR c.validTo > $timestamp)
                RETURN c.id as id, c.name as name, c.namespace as namespace, c.filePath as filePath
            ",
            new { repoId, timestamp });

            await foreach (var record in classResult)
            {
                classes.Add(new CodeClass(
                    record["id"].As<string>(),
                    record["name"].As<string>(),
                    record["namespace"].As<string>(),
                    record["filePath"].As<string>()
                ));
            }

            // Obtener métodos válidos en ese timestamp
            var methodResult = await tx.RunAsync(@"
                MATCH (m:Method {repoId: $repoId})
                WHERE m.validFrom <= $timestamp 
                  AND (m.validTo IS NULL OR m.validTo > $timestamp)
                RETURN m.id as id, m.name as name, m.classId as classId, 
                       m.filePath as filePath, m.body as body
            ",
            new { repoId, timestamp });

            await foreach (var record in methodResult)
            {
                methods.Add(new CodeMethod(
                    record["id"].As<string>(),
                    record["name"].As<string>(),
                    record["classId"].As<string>(),
                    record["filePath"].As<string>(),
                    record["body"].As<string>()
                ));
            }
        });

        return new GraphModel(classes, methods, edges, 
            new List<AspxPage>(), 
            new List<AspxControl>(), 
            new List<AspxEvent>());
    }

    /// <summary>
    /// Listar todas las versiones de un repositorio
    /// </summary>
    public async Task<List<VersionInfo>> GetVersionHistoryAsync(string repoId, CancellationToken ct)
    {
        await using var session = _driver.AsyncSession();
        var versions = new List<VersionInfo>();

        await session.ExecuteReadAsync(async tx =>
        {
            var result = await tx.RunAsync(@"
                MATCH (r:Repository {id: $repoId})-[:HAS_VERSION]->(v:Version)
                RETURN v.id as versionId, 
                       v.commitHash as commitHash, 
                       v.timestamp as timestamp,
                       v.isCurrent as isCurrent
                ORDER BY v.timestamp DESC
            ",
            new { repoId });

            await foreach (var record in result)
            {
                versions.Add(new VersionInfo(
                    record["versionId"].As<string>(),
                    record["commitHash"].As<string>(),
                    DateTimeOffset.FromUnixTimeSeconds(record["timestamp"].As<long>()),
                    record["isCurrent"].As<bool>()
                ));
            }
        });

        return versions;
    }

    public async ValueTask DisposeAsync()
    {
        if (_driver != null)
        {
            await _driver.DisposeAsync();
        }
    }
}
