using AriadnaKnowledgeStore.Core;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace AriadnaKnowledgeStore.Graph;

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

                // 5b. Cerrar versiones anteriores de RazorViews
                await tx.RunAsync(@"
                    MATCH (rv:RazorView {repoId: $repoId})
                    WHERE rv.validTo IS NULL
                    SET rv.validTo = $timestamp
                ",
                new { repoId, timestamp = timestamp.ToUnixTimeSeconds() });

                // 5c. Crear nuevas versiones de RazorViews
                foreach (var razorView in model.RazorViews)
                {
                    await tx.RunAsync(@"
                        CREATE (rv:RazorView {
                            id: $id,
                            versionId: $versionId,
                            name: $name,
                            filePath: $filePath,
                            modelType: $modelType,
                            layout: $layout,
                            injectedServices: $injectedServices,
                            repoId: $repoId,
                            validFrom: $timestamp,
                            validTo: null
                        })
                        WITH rv
                        MATCH (v:Version {id: $versionId})
                        MERGE (v)-[:CONTAINS]->(rv)

                        // Enlazar con versión anterior si existe
                        WITH rv
                        OPTIONAL MATCH (prev:RazorView {repoId: $repoId})
                        WHERE prev.id = $id 
                          AND prev.validTo = $timestamp
                          AND prev.versionId <> $versionId
                        FOREACH (_ IN CASE WHEN prev IS NOT NULL THEN [1] ELSE [] END |
                            MERGE (prev)-[:NEXT_VERSION]->(rv)
                        )
                    ",
                    new
                    {
                        id = razorView.Id,
                        versionId,
                        name = razorView.Name,
                        filePath = razorView.FilePath,
                        modelType = razorView.ModelType ?? "",
                        layout = razorView.Layout ?? "",
                        injectedServices = string.Join(",", razorView.InjectedServices),
                        repoId,
                        timestamp = timestamp.ToUnixTimeSeconds()
                    });
                }

                // 5d. Cerrar versiones anteriores de ViewComponents
                await tx.RunAsync(@"
                    MATCH (vc:ViewComponent {repoId: $repoId})
                    WHERE vc.validTo IS NULL
                    SET vc.validTo = $timestamp
                ",
                new { repoId, timestamp = timestamp.ToUnixTimeSeconds() });

                // 5e. Crear nuevas versiones de ViewComponents
                foreach (var component in model.ViewComponents)
                {
                    await tx.RunAsync(@"
                        CREATE (vc:ViewComponent {
                            id: $id,
                            versionId: $versionId,
                            name: $name,
                            invokedFrom: $invokedFrom,
                            repoId: $repoId,
                            validFrom: $timestamp,
                            validTo: null
                        })
                        WITH vc
                        MATCH (v:Version {id: $versionId})
                        MERGE (v)-[:CONTAINS]->(vc)

                        // Enlazar con versión anterior si existe
                        WITH vc
                        OPTIONAL MATCH (prev:ViewComponent {repoId: $repoId})
                        WHERE prev.id = $id 
                          AND prev.validTo = $timestamp
                          AND prev.versionId <> $versionId
                        FOREACH (_ IN CASE WHEN prev IS NOT NULL THEN [1] ELSE [] END |
                            MERGE (prev)-[:NEXT_VERSION]->(vc)
                        )
                    ",
                    new
                    {
                        id = component.Id,
                        versionId,
                        name = component.Name,
                        invokedFrom = component.InvokedFrom ?? "",
                        repoId,
                        timestamp = timestamp.ToUnixTimeSeconds()
                    });
                }

                // 5f. Cerrar versiones anteriores de ControllerActions
                await tx.RunAsync(@"
                    MATCH (ca:ControllerAction {repoId: $repoId})
                    WHERE ca.validTo IS NULL
                    SET ca.validTo = $timestamp
                ",
                new { repoId, timestamp = timestamp.ToUnixTimeSeconds() });

                // 5g. Crear nuevas versiones de ControllerActions
                foreach (var action in model.ControllerActions)
                {
                    await tx.RunAsync(@"
                        CREATE (ca:ControllerAction {
                            id: $id,
                            versionId: $versionId,
                            controllerName: $controllerName,
                            actionName: $actionName,
                            returnType: $returnType,
                            repoId: $repoId,
                            validFrom: $timestamp,
                            validTo: null
                        })
                        WITH ca
                        MATCH (v:Version {id: $versionId})
                        MERGE (v)-[:CONTAINS]->(ca)

                        // Enlazar con versión anterior si existe
                        WITH ca
                        OPTIONAL MATCH (prev:ControllerAction {repoId: $repoId})
                        WHERE prev.id = $id 
                          AND prev.validTo = $timestamp
                          AND prev.versionId <> $versionId
                        FOREACH (_ IN CASE WHEN prev IS NOT NULL THEN [1] ELSE [] END |
                            MERGE (prev)-[:NEXT_VERSION]->(ca)
                        )
                    ",
                    new
                    {
                        id = action.Id,
                        versionId,
                        controllerName = action.ControllerName,
                        actionName = action.ActionName,
                        returnType = action.ReturnType ?? "",
                        repoId,
                        timestamp = timestamp.ToUnixTimeSeconds()
                    });
                }

                // 5h. Cerrar versiones anteriores de BlazorComponents
                await tx.RunAsync(@"
                    MATCH (bc:BlazorComponent {repoId: $repoId})
                    WHERE bc.validTo IS NULL
                    SET bc.validTo = $timestamp
                ",
                new { repoId, timestamp = timestamp.ToUnixTimeSeconds() });

                // 5i. Crear nuevas versiones de BlazorComponents
                foreach (var blazorComponent in model.BlazorComponents)
                {
                    await tx.RunAsync(@"
                        CREATE (bc:BlazorComponent {
                            id: $id,
                            versionId: $versionId,
                            name: $name,
                            filePath: $filePath,
                            pageRoute: $pageRoute,
                            baseType: $baseType,
                            injectedServices: $injectedServices,
                            codeBlock: $codeBlock,
                            repoId: $repoId,
                            validFrom: $timestamp,
                            validTo: null
                        })
                        WITH bc
                        MATCH (v:Version {id: $versionId})
                        MERGE (v)-[:CONTAINS]->(bc)

                        // Enlazar con versión anterior si existe
                        WITH bc
                        OPTIONAL MATCH (prev:BlazorComponent {repoId: $repoId})
                        WHERE prev.id = $id 
                          AND prev.validTo = $timestamp
                          AND prev.versionId <> $versionId
                        FOREACH (_ IN CASE WHEN prev IS NOT NULL THEN [1] ELSE [] END |
                            MERGE (prev)-[:NEXT_VERSION]->(bc)
                        )
                    ",
                    new
                    {
                        id = blazorComponent.Id,
                        versionId,
                        name = blazorComponent.Name,
                        filePath = blazorComponent.FilePath,
                        pageRoute = blazorComponent.PageRoute ?? "",
                        baseType = blazorComponent.BaseType ?? "",
                        injectedServices = string.Join(",", blazorComponent.InjectedServices),
                        codeBlock = blazorComponent.CodeBlock ?? "",
                        repoId,
                        timestamp = timestamp.ToUnixTimeSeconds()
                    });
                }

                // 5j. Cerrar versiones anteriores de BlazorParameters
                await tx.RunAsync(@"
                    MATCH (bp:BlazorParameter {repoId: $repoId})
                    WHERE bp.validTo IS NULL
                    SET bp.validTo = $timestamp
                ",
                new { repoId, timestamp = timestamp.ToUnixTimeSeconds() });

                // 5k. Crear nuevas versiones de BlazorParameters
                foreach (var blazorParameter in model.BlazorParameters)
                {
                    await tx.RunAsync(@"
                        CREATE (bp:BlazorParameter {
                            id: $id,
                            versionId: $versionId,
                            componentId: $componentId,
                            name: $name,
                            type: $type,
                            isRequired: $isRequired,
                            repoId: $repoId,
                            validFrom: $timestamp,
                            validTo: null
                        })
                        WITH bp
                        MATCH (v:Version {id: $versionId})
                        MERGE (v)-[:CONTAINS]->(bp)

                        // Enlazar con versión anterior si existe
                        WITH bp
                        OPTIONAL MATCH (prev:BlazorParameter {repoId: $repoId})
                        WHERE prev.id = $id 
                          AND prev.validTo = $timestamp
                          AND prev.versionId <> $versionId
                        FOREACH (_ IN CASE WHEN prev IS NOT NULL THEN [1] ELSE [] END |
                            MERGE (prev)-[:NEXT_VERSION]->(bp)
                        )
                    ",
                    new
                    {
                        id = blazorParameter.Id,
                        versionId,
                        componentId = blazorParameter.ComponentId,
                        name = blazorParameter.Name,
                        type = blazorParameter.Type,
                        isRequired = blazorParameter.IsRequired,
                        repoId,
                        timestamp = timestamp.ToUnixTimeSeconds()
                    });
                }

                // 5l. Cerrar versiones anteriores de BlazorEventCallbacks
                await tx.RunAsync(@"
                    MATCH (bec:BlazorEventCallback {repoId: $repoId})
                    WHERE bec.validTo IS NULL
                    SET bec.validTo = $timestamp
                ",
                new { repoId, timestamp = timestamp.ToUnixTimeSeconds() });

                // 5m. Crear nuevas versiones de BlazorEventCallbacks
                foreach (var blazorCallback in model.BlazorEventCallbacks)
                {
                    await tx.RunAsync(@"
                        CREATE (bec:BlazorEventCallback {
                            id: $id,
                            versionId: $versionId,
                            componentId: $componentId,
                            name: $name,
                            eventType: $eventType,
                            repoId: $repoId,
                            validFrom: $timestamp,
                            validTo: null
                        })
                        WITH bec
                        MATCH (v:Version {id: $versionId})
                        MERGE (v)-[:CONTAINS]->(bec)

                        // Enlazar con versión anterior si existe
                        WITH bec
                        OPTIONAL MATCH (prev:BlazorEventCallback {repoId: $repoId})
                        WHERE prev.id = $id 
                          AND prev.validTo = $timestamp
                          AND prev.versionId <> $versionId
                        FOREACH (_ IN CASE WHEN prev IS NOT NULL THEN [1] ELSE [] END |
                            MERGE (prev)-[:NEXT_VERSION]->(bec)
                        )
                    ",
                    new
                    {
                        id = blazorCallback.Id,
                        versionId,
                        componentId = blazorCallback.ComponentId,
                        name = blazorCallback.Name,
                        eventType = blazorCallback.EventType ?? "",
                        repoId,
                        timestamp = timestamp.ToUnixTimeSeconds()
                    });
                }

                // 5n. Cerrar versiones anteriores de BlazorChildComponents
                await tx.RunAsync(@"
                    MATCH (bcc:BlazorChildComponent {repoId: $repoId})
                    WHERE bcc.validTo IS NULL
                    SET bcc.validTo = $timestamp
                ",
                new { repoId, timestamp = timestamp.ToUnixTimeSeconds() });

                // 5o. Crear nuevas versiones de BlazorChildComponents
                foreach (var blazorChild in model.BlazorChildComponents)
                {
                    await tx.RunAsync(@"
                        CREATE (bcc:BlazorChildComponent {
                            id: $id,
                            versionId: $versionId,
                            parentComponentId: $parentComponentId,
                            childComponentName: $childComponentName,
                            repoId: $repoId,
                            validFrom: $timestamp,
                            validTo: null
                        })
                        WITH bcc
                        MATCH (v:Version {id: $versionId})
                        MERGE (v)-[:CONTAINS]->(bcc)

                        // Enlazar con versión anterior si existe
                        WITH bcc
                        OPTIONAL MATCH (prev:BlazorChildComponent {repoId: $repoId})
                        WHERE prev.id = $id 
                          AND prev.validTo = $timestamp
                          AND prev.versionId <> $versionId
                        FOREACH (_ IN CASE WHEN prev IS NOT NULL THEN [1] ELSE [] END |
                            MERGE (prev)-[:NEXT_VERSION]->(bcc)
                        )
                    ",
                    new
                    {
                        id = blazorChild.Id,
                        versionId,
                        parentComponentId = blazorChild.ParentComponentId,
                        childComponentName = blazorChild.ChildComponentName,
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
        var blazorComponents = new List<BlazorComponent>();
        var blazorParameters = new List<BlazorParameter>();
        var blazorEventCallbacks = new List<BlazorEventCallback>();
        var blazorChildComponents = new List<BlazorChildComponent>();

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

            // Obtener BlazorComponents válidos en ese timestamp
            var blazorComponentResult = await tx.RunAsync(@"
                MATCH (bc:BlazorComponent {repoId: $repoId})
                WHERE bc.validFrom <= $timestamp 
                  AND (bc.validTo IS NULL OR bc.validTo > $timestamp)
                RETURN bc.id as id, bc.name as name, bc.filePath as filePath,
                       bc.pageRoute as pageRoute, bc.baseType as baseType,
                       bc.injectedServices as injectedServices, bc.codeBlock as codeBlock
            ",
            new { repoId, timestamp });

            await foreach (var record in blazorComponentResult)
            {
                var servicesStr = record["injectedServices"].As<string>();
                var services = string.IsNullOrEmpty(servicesStr) 
                    ? new List<string>() 
                    : servicesStr.Split(',').ToList();

                blazorComponents.Add(new BlazorComponent(
                    Id: record["id"].As<string>(),
                    Name: record["name"].As<string>(),
                    FilePath: record["filePath"].As<string>(),
                    PageRoute: record["pageRoute"].As<string>(),
                    BaseType: record["baseType"].As<string>(),
                    InjectedServices: services,
                    CodeBlock: record["codeBlock"].As<string>()
                ));
            }

            // Obtener BlazorParameters válidos en ese timestamp
            var blazorParameterResult = await tx.RunAsync(@"
                MATCH (bp:BlazorParameter {repoId: $repoId})
                WHERE bp.validFrom <= $timestamp 
                  AND (bp.validTo IS NULL OR bp.validTo > $timestamp)
                RETURN bp.id as id, bp.componentId as componentId, bp.name as name,
                       bp.type as type, bp.isRequired as isRequired
            ",
            new { repoId, timestamp });

            await foreach (var record in blazorParameterResult)
            {
                blazorParameters.Add(new BlazorParameter(
                    Id: record["id"].As<string>(),
                    ComponentId: record["componentId"].As<string>(),
                    Name: record["name"].As<string>(),
                    Type: record["type"].As<string>(),
                    IsRequired: record["isRequired"].As<bool>()
                ));
            }

            // Obtener BlazorEventCallbacks válidos en ese timestamp
            var blazorCallbackResult = await tx.RunAsync(@"
                MATCH (bec:BlazorEventCallback {repoId: $repoId})
                WHERE bec.validFrom <= $timestamp 
                  AND (bec.validTo IS NULL OR bec.validTo > $timestamp)
                RETURN bec.id as id, bec.componentId as componentId, bec.name as name,
                       bec.eventType as eventType
            ",
            new { repoId, timestamp });

            await foreach (var record in blazorCallbackResult)
            {
                blazorEventCallbacks.Add(new BlazorEventCallback(
                    Id: record["id"].As<string>(),
                    ComponentId: record["componentId"].As<string>(),
                    Name: record["name"].As<string>(),
                    EventType: record["eventType"].As<string>()
                ));
            }

            // Obtener BlazorChildComponents válidos en ese timestamp
            var blazorChildResult = await tx.RunAsync(@"
                MATCH (bcc:BlazorChildComponent {repoId: $repoId})
                WHERE bcc.validFrom <= $timestamp 
                  AND (bcc.validTo IS NULL OR bcc.validTo > $timestamp)
                RETURN bcc.id as id, bcc.parentComponentId as parentComponentId,
                       bcc.childComponentName as childComponentName
            ",
            new { repoId, timestamp });

            await foreach (var record in blazorChildResult)
            {
                blazorChildComponents.Add(new BlazorChildComponent(
                    Id: record["id"].As<string>(),
                    ParentComponentId: record["parentComponentId"].As<string>(),
                    ChildComponentName: record["childComponentName"].As<string>()
                ));
            }
        });

        return new GraphModel(classes, methods, edges, 
            new List<AspxPage>(), 
            new List<AspxControl>(), 
            new List<AspxEvent>(),
            // Razor collections (empty for now)
            new List<RazorView>(),
            new List<ViewComponent>(),
            new List<ControllerAction>(),
            // Blazor collections (now populated from Neo4j)
            blazorComponents,
            blazorParameters,
            blazorEventCallbacks,
            blazorChildComponents);
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
