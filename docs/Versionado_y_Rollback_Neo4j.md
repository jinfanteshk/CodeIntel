# Versionado y Rollback en Neo4j para CodeIntel

## Problema

El código inicial usaba `MERGE` que sobrescribe nodos y relaciones, **imposibilitando el rollback** a versiones anteriores cuando:
- Un commit/PR introduce errores en el análisis
- Se despliega código defectuoso a producción
- Un cliente necesita volver a un estado anterior del Knowledge Store

## Solución Implementada

### ✅ Versionado Temporal (Bitemporal) - **ÚNICA ESTRATEGIA**

**Archivo:** `Neo4jVersionedGraphStore.cs`

#### Cómo Funciona
- Cada nodo (Class, Method, AspxPage, AspxControl) tiene propiedades `validFrom` y `validTo` (timestamps)
- Las versiones antiguas NO se eliminan, se marcan como cerradas (`validTo` se setea)
- Cada versión tiene un nodo `Version` con metadata (commitHash, timestamp)
- Relaciones `NEXT_VERSION` conectan versiones consecutivas del mismo elemento

#### Modelo de Datos
```cypher
// Estructura
(Repository)-[:HAS_VERSION]->(Version)-[:CONTAINS]->(Class)
                                                      |
                                         [:NEXT_VERSION]
                                                      ↓
                                                   (Class_v2)

// Propiedades de nodos versionados
{
  id: "class:MyClass",
  versionId: "abc123",
  validFrom: 1704067200,  // Unix timestamp
  validTo: null,          // null = versión actual
  repoId: "owner/repo@main"
  // ... otros datos
}
```

#### Ventajas
✅ **Historial completo**: Puedes consultar el estado en cualquier punto del tiempo  
✅ **Diff entre versiones**: Comparar qué cambió entre commits  
✅ **Compacto**: Todo en una sola base de datos  
✅ **Queries temporales**: `WHERE validFrom <= $timestamp AND (validTo IS NULL OR validTo > $timestamp)`  
✅ **Auditabilidad**: Trazabilidad completa de cambios  
✅ **Soporte ASPX**: Incluye versionado de páginas y controles ASPX

#### Consideraciones
⚠️ El grafo crece con el tiempo (mitigable con políticas de retención)  
⚠️ Queries deben filtrar por validez temporal (automático en el código)

#### Casos de Uso
- ✅ Análisis forense: "¿Qué código existía cuando surgió el bug?"
- ✅ Compliance: Auditorías de cambios en el tiempo
- ✅ CI/CD: Comparar impacto de PR antes de merge
- ✅ Rollback quirúrgico: Volver solo algunos componentes

#### Ejemplo de Uso
```csharp
var store = new Neo4jVersionedGraphStore(uri, user, pass, logger);

// Guardar nueva versión (automáticamente cierra la anterior)
await store.UpsertAsync(repoRequest, graphModel, ct);

// Rollback completo a versión específica
await store.RollbackToVersionAsync(repoId, versionId, ct);

// Consultar estado histórico
var graphAtTime = await store.GetGraphAtTimestampAsync(repoId, timestamp, ct);

// Ver historial
var versions = await store.GetVersionHistoryAsync(repoId, ct);
// Output:
// - Version abc123 @ 2024-01-15 10:30 (current)
// - Version def456 @ 2024-01-14 15:20
// - Version ghi789 @ 2024-01-13 09:00
```

---

## Integración con Webhooks GitHub

Para actualización incremental vía webhooks:

```csharp
// GitHubWebhookFunction.cs
[Function("GitHubWebhook")]
public async Task<HttpResponseData> HandleWebhook(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
{
    // Parsear webhook payload
    var payload = await JsonSerializer.DeserializeAsync<GitHubPushEvent>(req.Body);

    // Crear nueva versión con commitHash
    var repoRequest = new RepoRequest(
        payload.Repository.Owner.Login,
        payload.Repository.Name,
        payload.Ref.Replace("refs/heads/", ""),
        Path: payload.HeadCommit.Id  // <-- CommitHash aquí
    );

    var graphModel = await _analyzer.AnalyzeAsync(localPath, ct);

    // Esto creará automáticamente una nueva versión
    await _versionedStore.UpsertAsync(repoRequest, graphModel, ct);

    return req.CreateResponse(HttpStatusCode.OK);
}
```

---

## Políticas de Retención

Para evitar que el grafo crezca indefinidamente:

```csharp
// Eliminar versiones más antiguas que 90 días
public async Task CleanupOldVersionsAsync(string repoId, int daysToKeep = 90)
{
    var cutoffTimestamp = DateTimeOffset.UtcNow.AddDays(-daysToKeep).ToUnixTimeSeconds();

    await using var session = _driver.AsyncSession();
    await session.ExecuteWriteAsync(async tx =>
    {
        // Eliminar nodos con validTo < cutoff
        await tx.RunAsync(@"
            MATCH (n)
            WHERE n.repoId = $repoId 
              AND n.validTo IS NOT NULL 
              AND n.validTo < $cutoff
            DETACH DELETE n
        ",
        new { repoId, cutoff = cutoffTimestamp });
    });
}
```

---

## Próximos Pasos

1. ✅ Implementar `Neo4jVersionedGraphStore` como default
2. ⬜ Agregar endpoint en Functions para listar versiones
3. ⬜ Crear UI para visualizar diff entre versiones
4. ⬜ Implementar políticas de retención configurables
5. ⬜ Agregar métricas: tamaño del grafo por versión, tiempo de rollback

---

## Ejemplo de Queries Útiles

### Ver qué clases cambiaron entre dos versiones
```cypher
MATCH (c1:Class {id: $classId})-[:NEXT_VERSION]->(c2:Class)
WHERE c1.versionId = $version1 AND c2.versionId = $version2
RETURN c1, c2
```

### Contar elementos por versión
```cypher
MATCH (v:Version {id: $versionId})-[:CONTAINS]->(c:Class)
RETURN count(c) as classCount
```

### Encontrar clases eliminadas
```cypher
MATCH (c:Class {repoId: $repoId})
WHERE c.validTo IS NOT NULL 
  AND NOT EXISTS((c)-[:NEXT_VERSION]->())
RETURN c.name, c.validTo
```
