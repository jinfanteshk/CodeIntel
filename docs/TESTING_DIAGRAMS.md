# 📊 DIAGRAMA - Flujo de Testing con Mocks

## 🎯 Arquitectura: Mock vs Neo4j

```
┌─────────────────────────────────────────────────────────────────┐
│                     CONFIGURACIÓN                                │
│                                                                   │
│  local.settings.json                                             │
│  ┌────────────────────────────────────────────────────────┐     │
│  │ {                                                       │     │
│  │   "GraphStore": {                                      │     │
│  │     "Type": "Mock"  ◄── CAMBIAR AQUÍ                  │     │
│  │   }                                                     │     │
│  │ }                                                       │     │
│  └────────────────────────────────────────────────────────┘     │
│                                                                   │
│         ┌──────────┐                  ┌──────────┐              │
│         │  "Mock"  │                  │ "Neo4j"  │              │
│         └─────┬────┘                  └─────┬────┘              │
│               │                             │                    │
└───────────────┼─────────────────────────────┼────────────────────┘
                │                             │
                ▼                             ▼
        ┌───────────────┐           ┌──────────────────┐
        │  MOCK MODE    │           │   NEO4J MODE     │
        │  🟢 SEGURO    │           │   🔵 PRODUCCIÓN  │
        └───────┬───────┘           └────────┬─────────┘
                │                            │
                └──────────┬─────────────────┘
                           │
                           ▼
                   ┌───────────────┐
                   │  Program.cs   │
                   │  DI Container │
                   └───────┬───────┘
                           │
         ┌─────────────────┼──────────────────┐
         │                 │                   │
         ▼                 ▼                   ▼
   [IGraphStore]   [IVectorIndex]   [IEmbeddingService]
         │                 │                   │
         │                 │                   │
    MOCK MODE              │              MOCK MODE
         │                 │                   │
         ▼                 ▼                   ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ MockGraphStore  │ │ MockVectorIndex │ │ MockEmbedding   │
│                 │ │                 │ │     Service     │
│ - En memoria    │ │ - En memoria    │ │                 │
│ - Logs [MOCK]   │ │ - Logs [MOCK]   │ │ - Fake vectors  │
│ - NO Neo4j ✅   │ │ - NO Neo4j ✅   │ │ - NO Azure ✅   │
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

---

## 🔄 Flujo de Ingesta con Mocks

```
┌──────────────────────────────────────────────────────────────────┐
│                    INGESTA PIPELINE                               │
└──────────────────────────────────────────────────────────────────┘

Usuario
  │
  │ POST /api/ingest
  │ {
  │   "owner": "test",
  │   "repo": "sample",
  │   "branch": "main",
  │   "path": "abc123"
  │ }
  │
  ▼
┌────────────────────────────────┐
│  IngestOrchestrator.RunAsync() │
└────────────────┬───────────────┘
                 │
                 │ ┌─────────────────────────────────────────┐
                 ├─┤ 1. Download Repository                  │
                 │ │    IGitHubSource ✅ REAL                │
                 │ │    ├─ Descarga de GitHub                │
                 │ │    └─ Guarda en C:\temp\...             │
                 │ └─────────────────────────────────────────┘
                 │
                 │ ┌─────────────────────────────────────────┐
                 ├─┤ 2. Analyze Code                         │
                 │ │    ICodeAnalyzer ✅ REAL                │
                 │ │    ├─ Roslyn para C#                    │
                 │ │    ├─ AspxAnalyzer                      │
                 │ │    ├─ RazorAnalyzer                     │
                 │ │    ├─ BlazorComponentAnalyzer           │
                 │ │    └─ Construye GraphModel              │
                 │ └─────────────────────────────────────────┘
                 │
                 │ ┌─────────────────────────────────────────┐
                 ├─┤ 3. Store Graph 🟢 MOCK                  │
                 │ │    IGraphStore → MockGraphStore         │
                 │ │    ├─ Genera VersionContext:            │
                 │ │    │  {                                 │
                 │ │    │    VersionId: "guid-123...",       │
                 │ │    │    RepoId: "test/sample@main",     │
                 │ │    │    Timestamp: 1704067200,          │
                 │ │    │    CommitHash: "abc123"            │
                 │ │    │  }                                 │
                 │ │    ├─ Log: [MOCK] Storing graph...      │
                 │ │    └─ NO toca Neo4j ✅                  │
                 │ └────────────┬──────────────────────────────┘
                 │              │
                 │              │ VersionContext
                 │              ▼
                 │ ┌─────────────────────────────────────────┐
                 ├─┤ 4. Generate Chunks                      │
                 │ │    CodeChunker.ToVectorDocs() ✅ REAL   │
                 │ │    ├─ Chunking de clases               │
                 │ │    ├─ Chunking de métodos              │
                 │ │    ├─ Chunking de ASPX                 │
                 │ │    ├─ Chunking de Razor                │
                 │ │    └─ Chunking de Blazor               │
                 │ └─────────────────────────────────────────┘
                 │
                 │ ┌─────────────────────────────────────────┐
                 ├─┤ 5. Generate Embeddings 🟢 MOCK         │
                 │ │    IEmbeddingService → MockEmbedding    │
                 │ │    ├─ Genera vectores fake [1536]      │
                 │ │    └─ NO llama a Azure OpenAI ✅       │
                 │ └─────────────────────────────────────────┘
                 │
                 │ ┌─────────────────────────────────────────┐
                 └─┤ 6. Index Vectors 🟢 MOCK               │
                   │    IVectorIndex → MockVectorIndex       │
                   │    ├─ Recibe VersionContext (MISMO!)   │
                   │    ├─ Acumula en memoria                │
                   │    ├─ Log: [MOCK] Indexed ... docs...  │
                   │    └─ NO toca Neo4j ✅                  │
                   └────────────┬──────────────────────────────┘
                                │
                                │ Response
                                ▼
                        ┌─────────────────┐
                        │   HTTP 200 OK   │
                        │   {             │
                        │     versionId,  │
                        │     timestamp,  │
                        │     commitHash, │
                        │     ...         │
                        │   }             │
                        └─────────────────┘
```

---

## 🧪 Testing Flow

```
┌────────────────────────────────────────────────────────────────┐
│                      TEST SCRIPT                                │
│                  test-versioning.ps1                            │
└────────────────────────────────────────────────────────────────┘
         │
         │
    ┌────┴────┬──────────┬──────────┬──────────┐
    │         │          │          │          │
    ▼         ▼          ▼          ▼          ▼
  TEST 1   TEST 2     TEST 3     TEST 4     TEST 5
    │         │          │          │          │
    │         │          │          │          │
    ▼         ▼          ▼          ▼          ▼

┌─────────────────────────────────────────────────────────────────┐
│ TEST 1: Primera Ingesta                                         │
├─────────────────────────────────────────────────────────────────┤
│ Request: {owner, repo, branch, path}                            │
│                                                                  │
│ ✅ Verificar:                                                   │
│   - Response incluye versionId                                  │
│   - Response incluye timestamp                                  │
│   - Response incluye commitHash                                 │
│   - Response incluye repo                                       │
│                                                                  │
│ Logs esperados:                                                 │
│   [MOCK] Storing graph: ... Version: guid-123                  │
│   ✅ Stored graph with version: guid-123                        │
│   [MOCK] Indexed ... documents for version guid-123            │
│   ✅ Indexed ... documents with version: guid-123               │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ TEST 2: Segunda Ingesta (Versión Única)                        │
├─────────────────────────────────────────────────────────────────┤
│ Request: {owner, repo, branch, path: "v2"}                      │
│                                                                  │
│ ✅ Verificar:                                                   │
│   - versionId diferente a Test 1                               │
│   - timestamp > Test 1                                          │
│                                                                  │
│ Comparación:                                                    │
│   V1: guid-123-aaa                                             │
│   V2: guid-456-bbb  ✅ DIFERENTE                               │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ TEST 3: Verificación de Campos                                 │
├─────────────────────────────────────────────────────────────────┤
│ ✅ Verificar campos presentes:                                 │
│   ✅ versionId                                                  │
│   ✅ timestamp                                                  │
│   ✅ commitHash                                                 │
│   ✅ repo                                                       │
│   ✅ classes                                                    │
│   ✅ methods                                                    │
│   ✅ indexed                                                    │
│   ✅ chunksGenerated                                            │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ TEST 4: Múltiples Repos                                        │
├─────────────────────────────────────────────────────────────────┤
│ Request A: org1/repoA                                           │
│ Request B: org2/repoB                                           │
│                                                                  │
│ ✅ Verificar:                                                   │
│   - Version A ≠ Version B                                      │
│   - Repo A ≠ Repo B                                            │
│   - Ambos tienen versionId único                               │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ TEST 5: Verificación Manual de Logs                            │
├─────────────────────────────────────────────────────────────────┤
│ Buscar en consola de Azure Function:                           │
│                                                                  │
│ ✅ [MOCK] Storing graph: ... Version: <guid>                   │
│ ✅ [MOCK] Indexed ... documents for version <guid>             │
│ ✅ El <guid> es EL MISMO en ambos logs                         │
│ ✅ Aparece: "✅ Stored graph with version: <guid>"              │
│ ✅ Aparece: "✅ Indexed ... documents with version: <guid>"     │
└─────────────────────────────────────────────────────────────────┘

         │
         │ ┌─────────────────┐
         └─┤ RESULTADO FINAL │
           └────────┬────────┘
                    │
              ┌─────┴─────┐
              │           │
        TODOS PASARON   ALGUNO FALLÓ
              │           │
              ▼           ▼
        ┌───────────┐ ┌─────────────┐
        │ ✅ ÉXITO  │ │ ❌ REVISAR  │
        │           │ │             │
        │ Listo     │ │ Debugging   │
        │ para      │ │ requerido   │
        │ Neo4j     │ │             │
        └───────────┘ └─────────────┘
```

---

## 🔄 Transición Mock → Neo4j

```
┌────────────────────────────────────────────────────────────┐
│                    FASE 1: MOCK                             │
│                  (Desarrollo Seguro)                        │
└────────────────────────────────────────────────────────────┘

    GraphStore:Type = "Mock"
          │
          │ ✅ Tests pasan
          │ ✅ Logs correctos
          │ ✅ Sin errores
          │
          ▼
    ┌───────────────┐
    │ CONFIANZA ✅  │
    └───────┬───────┘
            │
            │ Cambiar configuración
            │
            ▼

┌────────────────────────────────────────────────────────────┐
│                  FASE 2: NEO4J                              │
│               (Persistencia Real)                           │
└────────────────────────────────────────────────────────────┘

    GraphStore:Type = "Neo4jVersioned"
          │
          │ Re-ejecutar tests
          │
          ▼
    ┌───────────────────────┐
    │ Verificar en Neo4j:   │
    │                       │
    │ MATCH (v:Version)     │
    │ RETURN v              │
    │                       │
    │ MATCH (n:CodeNode)    │
    │ RETURN n.versionId,   │
    │        count(*)       │
    └───────────────────────┘
```

---

## 🧩 VersionContext Flow

```
┌──────────────────────────────────────────────────────────────┐
│                  VersionContext Journey                       │
└──────────────────────────────────────────────────────────────┘

┌────────────────┐
│ MockGraphStore │
│  UpsertAsync() │
└───────┬────────┘
        │
        │ Genera VersionContext
        │ {
        │   VersionId: "guid-abc-123...",
        │   RepoId: "test/sample@main",
        │   Timestamp: 1704067200,
        │   CommitHash: "abc123"
        │ }
        │
        ▼
┌───────────────────┐
│ return versionCtx │◄──┐
└────────┬──────────┘   │
         │               │ MISMO OBJETO
         │               │
         ▼               │
┌────────────────────┐  │
│  Orchestrator      │  │
│  var ctx = await   │  │
│    graph.Upsert()  │  │
└────────┬───────────┘  │
         │               │
         │ Pasa contexto │
         │               │
         ▼               │
┌─────────────────────┐ │
│  MockVectorIndex    │ │
│  UpsertAsync(       │ │
│    docs,            │ │
│    versionCtx, ◄────┘
│    ct               │
│  )                  │
└─────────────────────┘

✅ RESULTADO: Graph y Vector usan el MISMO versionId
```

---

## 📊 Estado de Componentes

```
┌────────────────────────────────────────────────────────────┐
│                   SISTEMA COMPLETO                          │
└────────────────────────────────────────────────────────────┘

┌─────────────────┬──────────────┬────────────────────────────┐
│   COMPONENTE    │    ESTADO    │       OBSERVACIONES        │
├─────────────────┼──────────────┼────────────────────────────┤
│ MockGraphStore  │ ✅ Listo     │ Genera VersionContext      │
│                 │              │ Logs correctos             │
│                 │              │ NO usa Neo4j               │
├─────────────────┼──────────────┼────────────────────────────┤
│ MockVectorIndex │ ✅ Listo     │ Acepta VersionContext      │
│                 │              │ Logs correctos             │
│                 │              │ NO usa Neo4j               │
├─────────────────┼──────────────┼────────────────────────────┤
│ MockEmbedding   │ ✅ Listo     │ Genera vectores fake       │
│ Service         │              │ NO usa Azure OpenAI        │
├─────────────────┼──────────────┼────────────────────────────┤
│ Neo4jVersioned  │ ✅ Listo     │ Para producción            │
│ GraphStore      │              │ Usa cuando Type="Neo4j"    │
├─────────────────┼──────────────┼────────────────────────────┤
│ Neo4jVector     │ ✅ Listo     │ Para producción            │
│ Index           │              │ Usa cuando Type="Neo4j"    │
├─────────────────┼──────────────┼────────────────────────────┤
│ Orchestrator    │ ✅ Listo     │ Pasa VersionContext        │
│                 │              │ Logs detallados            │
├─────────────────┼──────────────┼────────────────────────────┤
│ Configuration   │ ✅ Listo     │ Switch Mock/Neo4j          │
│ DI              │              │ Sin cambios de código      │
├─────────────────┼──────────────┼────────────────────────────┤
│ Test Script     │ ✅ Listo     │ test-versioning.ps1        │
│                 │              │ 5 tests automatizados      │
├─────────────────┼──────────────┼────────────────────────────┤
│ Documentation   │ ✅ Completa  │ 5 archivos creados         │
└─────────────────┴──────────────┴────────────────────────────┘

🟢 TODO LISTO PARA TESTING
```

---

**Creado:** 2024  
**Propósito:** Visualización del flujo de testing  
**Diagramas:** ASCII art para documentación
