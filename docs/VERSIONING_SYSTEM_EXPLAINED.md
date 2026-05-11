# 🕐 Sistema de Versionado Temporal - Explicación Completa

Este documento explica en detalle cómo funciona el sistema de versionado implementado en AriadnaKnowledgeStore.

---

## 📋 **Conceptos Clave**

### **1. ¿Qué es el Versionado Temporal?**

Es un sistema que mantiene **TODAS las versiones históricas** de tu código en Neo4j, permitiéndote:
- Ver cómo era el código en cualquier momento del pasado
- Hacer rollback a versiones anteriores
- Comparar cambios entre versiones
- Auditar evolución del código

### **2. ¿Depende de versiones de GitHub?**

**NO directamente**, pero puede integrarse. El sistema usa:
- **`versionId`**: GUID único generado automáticamente por el sistema
- **`commitHash`**: Puede contener el SHA de commit de GitHub (opcional)
- **`timestamp`**: Unix timestamp cuando se ingirió el código

**Flujo:**
```
GitHub Commit → Webhook/Trigger → Ingesta → Nueva Versión en Neo4j
```

---

## 🏗️ **Arquitectura del Sistema**

### **Estructura de Nodos en Neo4j:**

```
Repository
    ├── HAS_VERSION → Version (v1) [isCurrent: false]
    │                     └── CONTAINS → Class (v1) [validFrom: t1, validTo: t2]
    │                                   └── NEXT_VERSION → Class (v2)
    ├── HAS_VERSION → Version (v2) [isCurrent: false]
    │                     └── CONTAINS → Class (v2) [validFrom: t2, validTo: t3]
    │                                   └── NEXT_VERSION → Class (v3)
    └── HAS_VERSION → Version (v3) [isCurrent: true]  ← Versión actual
                          └── CONTAINS → Class (v3) [validFrom: t3, validTo: NULL]
```

### **Propiedades de Versionado:**

#### **Nodo `Version`:**
```json
{
  "id": "guid-unique",              // GUID generado automáticamente
  "repoId": "owner/repo@branch",    // Identificador del repositorio
  "commitHash": "abc123...",        // SHA de commit de GitHub (opcional)
  "timestamp": 1704067200,          // Unix timestamp (cuando se ingirió)
  "isCurrent": true                 // ¿Es la versión activa?
}
```

#### **Nodo `Class` (ejemplo):**
```json
{
  "id": "class:MyNamespace.Product",
  "versionId": "guid-unique",       // A qué versión pertenece
  "repoId": "owner/repo@branch",
  "validFrom": 1704067200,          // Desde cuándo existe
  "validTo": null,                  // Hasta cuándo existe (NULL = actual)
  "name": "Product",
  "namespace": "MyNamespace",
  "filePath": "Models/Product.cs"
}
```

---

## 🔄 **Proceso de Versionado (Paso a Paso)**

### **Cuando se ingiere código nuevo:**

#### **Paso 1: Crear Nodo de Versión**
```cypher
// Se crea un snapshot completo del repositorio
CREATE (v:Version {
    id: "guid-123",
    repoId: "acme/shop@main",
    commitHash: "abc123def456",
    timestamp: 1704067200,
    isCurrent: true
})

// Marcar versión anterior como no-current
MATCH (oldV:Version {isCurrent: true})
WHERE oldV.id <> "guid-123"
SET oldV.isCurrent = false
```

#### **Paso 2: Cerrar Versiones Anteriores (Soft Delete)**
```cypher
// Marcar todos los nodos actuales con validTo
MATCH (c:Class {repoId: "acme/shop@main"})
WHERE c.validTo IS NULL
SET c.validTo = 1704067200  // Ahora tienen fecha de expiración
```

#### **Paso 3: Crear Nuevas Versiones de Nodos**
```cypher
// Crear nueva versión del nodo Class
CREATE (c:Class {
    id: "class:Product",
    versionId: "guid-123",
    validFrom: 1704067200,
    validTo: null,              // NULL = versión activa
    name: "Product",
    namespace: "Models",
    filePath: "Models/Product.cs"
})

// Enlazar con versión anterior (historial)
MATCH (prev:Class {id: "class:Product"})
WHERE prev.validTo = 1704067200
MERGE (prev)-[:NEXT_VERSION]->(c)
```

---

## 📊 **Ejemplo Visual Completo**

### **Escenario: 3 ingestas de código**

#### **Estado Inicial (t0):**
```
Neo4j: Vacío
```

#### **Ingesta #1 (t1 = 2024-01-01 10:00:00):**
```
Código ingresado:
- Product.cs (10 líneas)
- Order.cs (20 líneas)

Neo4j después:
Repository "acme/shop@main"
  └── Version (v1) [isCurrent: true, timestamp: t1]
         ├── Class "Product" [validFrom: t1, validTo: NULL]
         └── Class "Order" [validFrom: t1, validTo: NULL]
```

#### **Ingesta #2 (t2 = 2024-01-02 15:30:00):**
```
Código modificado:
- Product.cs (15 líneas) ← MODIFICADO
- Order.cs (20 líneas)    ← Sin cambios
- Payment.cs (30 líneas)  ← NUEVO

Neo4j después:
Repository "acme/shop@main"
  ├── Version (v1) [isCurrent: false, timestamp: t1]
  │      ├── Class "Product" [validFrom: t1, validTo: t2] ←─┐
  │      └── Class "Order" [validFrom: t1, validTo: t2]      │
  │                                                            │ NEXT_VERSION
  └── Version (v2) [isCurrent: true, timestamp: t2]          │
         ├── Class "Product" [validFrom: t2, validTo: NULL] ←┘
         ├── Class "Order" [validFrom: t2, validTo: NULL]
         └── Class "Payment" [validFrom: t2, validTo: NULL] ← NUEVO
```

#### **Ingesta #3 (t3 = 2024-01-03 09:00:00):**
```
Código modificado:
- Product.cs (15 líneas)   ← Sin cambios
- Order.cs (ELIMINADO)     ← BORRADO
- Payment.cs (35 líneas)   ← MODIFICADO

Neo4j después:
Repository "acme/shop@main"
  ├── Version (v1) [isCurrent: false]
  │      ├── Class "Product" [validFrom: t1, validTo: t2]
  │      └── Class "Order" [validFrom: t1, validTo: t2]
  │
  ├── Version (v2) [isCurrent: false]
  │      ├── Class "Product" [validFrom: t2, validTo: t3]
  │      ├── Class "Order" [validFrom: t2, validTo: t3] ← Ya no existe en v3
  │      └── Class "Payment" [validFrom: t2, validTo: t3]
  │
  └── Version (v3) [isCurrent: true]
         ├── Class "Product" [validFrom: t3, validTo: NULL]
         └── Class "Payment" [validFrom: t3, validTo: NULL]
         // Order NO aparece aquí (fue eliminado)
```

---

## 🔙 **Sistema de Rollback**

### **Tipos de Rollback:**

#### **1. Rollback "Lógico" (Cambiar Puntero)**
```csharp
// Marcar v2 como versión actual
await store.RollbackToVersionAsync("acme/shop@main", "version-v2-guid", ct);
```

**Qué hace:**
```cypher
// Solo cambia el flag isCurrent
MATCH (r:Repository {id: "acme/shop@main"})
MATCH (r)-[:HAS_VERSION]->(targetV:Version {id: "version-v2-guid"})
SET targetV.isCurrent = true

// Desmarca la actual
MATCH (currentV:Version {isCurrent: true})
WHERE currentV.id <> "version-v2-guid"
SET currentV.isCurrent = false
```

**Resultado:**
- ✅ La versión v2 se marca como "current"
- ✅ Las queries `WHERE validTo IS NULL` ahora NO traen v3
- ✅ Los nodos de v3 **siguen existiendo** (no se borran)
- ✅ Puedes volver a v3 cuando quieras

#### **2. Consulta Histórica (Time Travel)**
```csharp
// Ver cómo era el código el 2024-01-02
var model = await store.GetGraphAtTimestampAsync(
    "acme/shop@main", 
    timestamp: 1704211800,  // 2024-01-02 15:30:00
    ct
);
```

**Qué devuelve:**
```
GraphModel con:
- Product.cs (versión de t2)
- Order.cs (versión de t2) ← Aunque ya no existe en t3
- Payment.cs (versión de t2)
```

**Query Neo4j ejecutada:**
```cypher
MATCH (c:Class {repoId: "acme/shop@main"})
WHERE c.validFrom <= 1704211800           // Existía en ese momento
  AND (c.validTo IS NULL                   // Todavía vigente, O
       OR c.validTo > 1704211800)          // Expiró después
RETURN c
```

---

## 🎯 **Casos de Uso Reales**

### **Caso 1: Detectar Cuándo Se Introdujo un Bug**

**Problema:** Un bug apareció en producción el 5 de enero.

**Solución:**
```csharp
// Listar todas las versiones
var versions = await store.GetVersionHistoryAsync("acme/shop@main", ct);

// versions contiene:
// v1: 2024-01-01 10:00 (commit: abc123)
// v2: 2024-01-02 15:30 (commit: def456)
// v3: 2024-01-03 09:00 (commit: ghi789) ← Posible culpable
// v4: 2024-01-05 14:20 (commit: jkl012)

// Comparar v3 vs v2 para ver qué cambió
var graphT2 = await store.GetGraphAtTimestampAsync("acme/shop@main", v2.Timestamp, ct);
var graphT3 = await store.GetGraphAtTimestampAsync("acme/shop@main", v3.Timestamp, ct);

// Comparar diferencias...
```

**Query Neo4j para ver cambios entre versiones:**
```cypher
// ¿Qué clases se modificaron entre v2 y v3?
MATCH (v2:Version {id: "version-v2-guid"})-[:CONTAINS]->(c2:Class)
MATCH (v3:Version {id: "version-v3-guid"})-[:CONTAINS]->(c3:Class)
WHERE c2.id = c3.id
  AND c2.filePath <> c3.filePath  // Cambió la ruta
RETURN c2.name, c2.filePath as old, c3.filePath as new
```

---

### **Caso 2: Rollback de Emergencia**

**Problema:** Deployment de v4 causó error crítico en producción.

**Solución:**
```csharp
// 1. Listar versiones
var versions = await store.GetVersionHistoryAsync("acme/shop@main", ct);

// 2. Identificar la última versión estable (v3)
var stableVersion = versions.First(v => v.VersionId == "version-v3-guid");

// 3. Hacer rollback lógico
await store.RollbackToVersionAsync("acme/shop@main", stableVersion.VersionId, ct);

// 4. Ahora el sistema apunta a v3
// Las queries traerán la versión estable del código
```

**Impacto:**
- ✅ GraphRAG inmediatamente usa la versión v3
- ✅ Las búsquedas vectoriales siguen funcionando (todos los chunks existen)
- ✅ Las dependencias del grafo reflejan el estado de v3
- ✅ No se pierde información de v4 (puedes volver si quieres)

---

### **Caso 3: Auditoría de Cambios**

**Pregunta:** ¿Cuándo se eliminó la clase `OrderValidator`?

**Query:**
```cypher
// Buscar todas las versiones de OrderValidator
MATCH (c:Class)
WHERE c.id = "class:OrderValidator"
  AND c.repoId = "acme/shop@main"
RETURN c.versionId, c.validFrom, c.validTo
ORDER BY c.validFrom

// Resultado:
// v1: validFrom: t1, validTo: t2  (existió)
// v2: validFrom: t2, validTo: t3  (existió)
// v3: validFrom: t3, validTo: t4  (existió)
// (No aparece en v4) ← Se eliminó en v4
```

**Conclusión:** Se eliminó entre v3 (2024-01-03) y v4 (2024-01-05).

---

## 🔧 **Integración con GitHub**

### **Opción A: Manual (Estado Actual)**

```csharp
// En tu función de ingesta
var repoRequest = new RepoRequest(
    Owner: "acme",
    Repo: "shop",
    Branch: "main",
    Path: "abc123def456"  // ← Commit SHA de GitHub
);

await ingestFunction.IngestAsync(repoRequest, ct);

// Neo4j almacena:
// Version { commitHash: "abc123def456", timestamp: <now> }
```

### **Opción B: Webhook Automático (Futuro)**

```csharp
// GitHub webhook dispara Azure Function
[Function("GitHubWebhook")]
public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
{
    var payload = await req.ReadFromJsonAsync<GitHubWebhookPayload>();

    var repoRequest = new RepoRequest(
        Owner: payload.Repository.Owner,
        Repo: payload.Repository.Name,
        Branch: payload.Ref.Replace("refs/heads/", ""),
        Path: payload.After  // ← Commit SHA del push
    );

    // Esto crea una nueva versión automáticamente
    await _ingestFunction.IngestAsync(repoRequest, ct);

    return req.CreateResponse(HttpStatusCode.OK);
}
```

---

## 📊 **Queries de Versionado Útiles**

### **1. Listar todas las versiones de un repositorio:**
```cypher
MATCH (r:Repository {id: "acme/shop@main"})-[:HAS_VERSION]->(v:Version)
RETURN v.id, v.commitHash, v.timestamp, v.isCurrent
ORDER BY v.timestamp DESC
```

### **2. Ver qué versión está activa:**
```cypher
MATCH (r:Repository {id: "acme/shop@main"})-[:HAS_VERSION]->(v:Version {isCurrent: true})
RETURN v.id, v.commitHash, v.timestamp
```

### **3. Ver historial de una clase específica:**
```cypher
MATCH (c:Class {id: "class:Product"})
WHERE c.repoId = "acme/shop@main"
OPTIONAL MATCH (c)-[:NEXT_VERSION*]->(newer:Class)
OPTIONAL MATCH (older:Class)-[:NEXT_VERSION*]->(c)
RETURN older, c, newer
ORDER BY c.validFrom
```

### **4. Comparar dos versiones:**
```cypher
// Clases que existían en v1 pero no en v2
MATCH (v1:Version {id: "version-v1-guid"})-[:CONTAINS]->(c1:Class)
WHERE NOT EXISTS {
    MATCH (v2:Version {id: "version-v2-guid"})-[:CONTAINS]->(c2:Class {id: c1.id})
}
RETURN c1.name, c1.filePath
```

### **5. Ver componentes Blazor activos en una versión específica:**
```cypher
MATCH (v:Version {id: "version-guid"})-[:CONTAINS]->(bc:BlazorComponent)
RETURN bc.name, bc.pageRoute, bc.filePath
ORDER BY bc.name
```

---

## ⚙️ **Configuración y Mantenimiento**

### **Limpieza de Versiones Antiguas (Opcional):**

Si quieres eliminar versiones muy antiguas para ahorrar espacio:

```cypher
// Eliminar versiones más antiguas que 90 días
MATCH (v:Version)
WHERE v.timestamp < (timestamp() / 1000) - (90 * 24 * 60 * 60)
  AND v.isCurrent = false
DETACH DELETE v
```

### **Tamaño del Historial:**

Cada versión completa ocupa espacio en Neo4j. Para proyectos grandes:
- **Opción 1:** Mantener solo últimas N versiones
- **Opción 2:** Hacer snapshots semanales en lugar de cada commit
- **Opción 3:** Comprimir versiones antiguas (solo diff)

---

## 🎯 **Resumen: Preguntas Frecuentes**

### **¿Depende de versiones de GitHub?**
❌ No directamente. Usa su propio sistema de versionado con GUIDs.
✅ Puede almacenar el commit SHA de GitHub como metadata.

### **¿Cómo se hace rollback?**
✅ Lógico: Cambiar el flag `isCurrent` (instantáneo, reversible)
✅ Time-travel: Consultar nodos válidos en un timestamp específico

### **¿Se pierden datos al hacer rollback?**
❌ NO. Los nodos de versiones futuras siguen existiendo.
✅ Solo cambias qué versión se considera "actual".

### **¿Cómo se usa?**
```csharp
// Ingerir código → Crea nueva versión automáticamente
await ingestFunction.IngestAsync(repoRequest, ct);

// Listar versiones
var versions = await store.GetVersionHistoryAsync("acme/shop@main", ct);

// Rollback
await store.RollbackToVersionAsync("acme/shop@main", versionId, ct);

// Time-travel
var oldGraph = await store.GetGraphAtTimestampAsync("acme/shop@main", timestamp, ct);
```

---

## 📚 **Siguiente Lectura:**

- `BLAZOR_NEO4J_QUERIES.md` - Queries específicas para componentes Blazor
- `README.md` - Arquitectura general del sistema
- `neo4j-graphrag-architecture.md` - Detalles de implementación GraphRAG

---

**Última actualización:** 2024
**Versión del documento:** 1.0
