# Guía de Prueba Local - CodeIntel

## 🎯 Objetivo

Ejecutar la función de ingesta localmente y verificar que los nodos se crean en Neo4j AuraDB.

---

## ✅ Prerrequisitos

### 1. Configuración Verificada

Tu `local.settings.json` debe tener (ya está configurado):

```json
{
  "Values": {
    "GitHub:Token": "ghp_...",  // ✅ Tu token de GitHub
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

Tu `appsettings.json` ya tiene:

```json
{
  "GraphStore": {
    "Type": "Neo4jVersioned"  // ✅ Usando Neo4j con versionado
  },
  "Neo4j": {
    "Uri": "neo4j+s://503c34a2.databases.neo4j.io",
    "User": "neo4j",
    "Password": "Es5RDiQ-5yfsz76r70iyNTqvIHKj_ueyKbGqZNSm9NU"
  },
  "AzureOpenAI": {
    "Endpoint": "",  // ✅ Vacío = usará MockEmbeddingService
    "ApiKey": ""
  }
}
```

### 2. Azure Functions Core Tools

Verifica que lo tienes instalado:

```powershell
func --version
# Debe mostrar: 4.x.x
```

Si no lo tienes, instálalo:

```powershell
winget install Microsoft.Azure.FunctionsCoreTools
```

---

## 🚀 Paso 1: Iniciar Azure Functions Localmente

### Desde Visual Studio 2026

**Opción A: Usando F5 (Debug)**

1. En Visual Studio, abre el Solution Explorer
2. Haz clic derecho en el proyecto `CodeIntel.Functions`
3. Selecciona **"Set as Startup Project"**
4. Presiona **F5** o **Ctrl+F5**
5. Visual Studio iniciará el host de Functions

**Opción B: Desde Terminal en Visual Studio**

1. En Visual Studio, abre **View → Terminal** (o `Ctrl+`)
2. Navega al proyecto:

```powershell
cd CodeIntel.Functions
```

3. Inicia Functions:

```powershell
func start
```

### Salida Esperada

Deberías ver algo como:

```
Azure Functions Core Tools
Core Tools Version:       4.x.x
Function Runtime Version: 4.x.x

Functions:

        IngestFunction: [POST] http://localhost:7071/api/ingest

        GitHubWebhookFunction: [POST] http://localhost:7071/api/webhook

        GetVersionHistory: [GET] http://localhost:7071/api/versions

        RollbackToVersion: [POST] http://localhost:7071/api/rollback/{versionId}

        GetGraphAtTime: [GET] http://localhost:7071/api/snapshot

For detailed output, run func with --verbose flag.
```

---

## 🔥 Paso 2: Llamar a la Función de Ingesta

### Método 1: Usando PowerShell (Recomendado)

Abre una **nueva terminal** (deja el `func start` corriendo) y ejecuta:

```powershell
# Ingesta de un repositorio de ejemplo (pequeño para prueba)
$body = @{
    owner = "octocat"
    repo = "Hello-World"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" `
    -Method POST `
    -Body $body `
    -ContentType "application/json"
```

**Alternativa: Tu propio repositorio**

```powershell
$body = @{
    owner = "jesusinfantes-hkteck"
    repo = "CodeIntel"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" `
    -Method POST `
    -Body $body `
    -ContentType "application/json"
```

### Método 2: Usando curl

```bash
curl -X POST http://localhost:7071/api/ingest \
  -H "Content-Type: application/json" \
  -d '{"owner":"octocat","repo":"Hello-World"}'
```

### Método 3: Usando REST Client en VS Code

Si usas la extensión **REST Client**, crea un archivo `test.http`:

```http
### Ingesta de repositorio
POST http://localhost:7071/api/ingest
Content-Type: application/json

{
  "owner": "octocat",
  "repo": "Hello-World"
}
```

---

## 📊 Paso 3: Verificar el Progreso

### En la Terminal de Functions

Deberías ver logs como:

```
[2026-05-07T15:30:00.123Z] Executing 'IngestFunction' (Reason='This function was programmatically called via the host APIs.', Id=abc-123)
[2026-05-07T15:30:00.234Z] IngestOrchestrator: Starting ingestion for octocat/Hello-World
[2026-05-07T15:30:00.345Z] OctokitGitHubSource: Downloading repository octocat/Hello-World
[2026-05-07T15:30:02.456Z] RoslynAnalyzer: Analyzing 15 C# files
[2026-05-07T15:30:03.567Z] Neo4jVersionedGraphStore: Upserting graph model (version: v_20260507_153003)
[2026-05-07T15:30:04.678Z] Neo4jVectorIndex: Ensuring vector index exists
[2026-05-07T15:30:04.789Z] MockEmbeddingService: Generating mock embeddings for 45 documents
[2026-05-07T15:30:05.890Z] Neo4jVectorIndex: Upserting 45 vector documents
[2026-05-07T15:30:07.001Z] IngestOrchestrator: Ingestion completed successfully
[2026-05-07T15:30:07.112Z] Executed 'IngestFunction' (Succeeded, Id=abc-123, Duration=6989ms)
```

---

## 🔍 Paso 4: Verificar los Nodos en Neo4j

### Opción A: Neo4j Browser (Recomendado)

1. Ve a: https://console.neo4j.io
2. Inicia sesión y selecciona tu instancia
3. Haz clic en **"Open with"** → **"Browser"**
4. Ejecuta estas queries:

#### Verificar que existen nodos

```cypher
// Contar todos los nodos
MATCH (n)
RETURN labels(n) as tipo, count(n) as cantidad
ORDER BY cantidad DESC
```

**Resultado esperado:**

```
tipo              | cantidad
------------------|----------
["CodeNode"]      | 45
["Method"]        | 30
["Class"]         | 12
["Repository"]    | 1
["Version"]       | 1
```

#### Ver la estructura del grafo

```cypher
// Ver una muestra del grafo
MATCH (r:Repository)-[*1..2]-(n)
RETURN r, n
LIMIT 50
```

#### Verificar el índice vectorial

```cypher
// Ver los índices creados
SHOW INDEXES
YIELD name, type, labelsOrTypes, properties, state
WHERE type = 'VECTOR'
RETURN name, labelsOrTypes, properties, state
```

**Resultado esperado:**

```
name              | labelsOrTypes | properties    | state
------------------|---------------|---------------|--------
code_embeddings   | ["CodeNode"]  | ["embedding"] | ONLINE
```

#### Ver nodos con embeddings

```cypher
// Verificar que los nodos tienen embeddings
MATCH (n:CodeNode)
WHERE n.embedding IS NOT NULL
RETURN n.id, n.type, n.className, size(n.embedding) as embeddingDimensions
LIMIT 10
```

#### Ver el historial de versiones

```cypher
// Ver todas las versiones creadas
MATCH (v:Version)
RETURN v.id, v.timestamp, v.repository, v.commit
ORDER BY v.timestamp DESC
```

#### Ver métodos y sus relaciones

```cypher
// Ver métodos que llaman a otros métodos
MATCH (m1:Method)-[:CALLS]->(m2:Method)
RETURN m1.name as caller, m2.name as called
LIMIT 20
```

### Opción B: Cypher Shell (CLI)

Si prefieres usar la línea de comandos:

```bash
# Conectarse a Neo4j AuraDB
cypher-shell -a neo4j+s://503c34a2.databases.neo4j.io \
  -u neo4j \
  -p "Es5RDiQ-5yfsz76r70iyNTqvIHKj_ueyKbGqZNSm9NU"

# Ejecutar queries
neo4j> MATCH (n) RETURN count(n);
```

---

## 🧪 Paso 5: Probar GraphRAG (Búsqueda Vectorial + Grafo)

### Query Manual de GraphRAG

En Neo4j Browser, ejecuta:

```cypher
// Simulación de búsqueda vectorial + traversal
// (Usa un embedding mock para la demo)
WITH [0.1, 0.2, 0.3] AS mockEmbedding  // En producción, esto viene del user prompt

// 1. Buscar nodos similares (simulado)
MATCH (n:CodeNode)
WHERE n.type = 'Method'
WITH n LIMIT 5

// 2. Traversar el grafo para obtener contexto
MATCH (n)-[:REPRESENTS]->(method:Method)
OPTIONAL MATCH (method)-[:CALLS|DEPENDS_ON*1..2]-(related)

RETURN 
  n.content as code,
  method.name as methodName,
  collect(DISTINCT related.name) as relatedEntities
LIMIT 5
```

---

## 🐛 Troubleshooting

### Error: "Could not connect to Neo4j"

**Síntomas:**
```
Neo4j.Driver.ServiceUnavailableException: Connection to the database terminated
```

**Solución:**

1. Verifica que tu instancia de Neo4j AuraDB esté activa:
   - Ve a https://console.neo4j.io
   - Verifica que el estado sea "Running"

2. Verifica las credenciales en `appsettings.json`:
   ```json
   "Neo4j": {
     "Uri": "neo4j+s://503c34a2.databases.neo4j.io",  // Debe usar neo4j+s://
     "User": "neo4j",
     "Password": "..."  // Verifica que sea correcto
   }
   ```

3. Prueba la conexión manualmente:
   ```powershell
   # Instala Neo4j.Driver.Simple si no lo tienes
   dotnet add package Neo4j.Driver.Simple

   # Prueba de conexión
   $driver = [Neo4j.Driver.GraphDatabase]::Driver(
       "neo4j+s://503c34a2.databases.neo4j.io",
       [Neo4j.Driver.AuthTokens]::Basic("neo4j", "tu-password")
   )
   $session = $driver.AsyncSession()
   ```

### Error: "GitHub API rate limit"

**Síntomas:**
```
Octokit.RateLimitExceededException: API rate limit exceeded
```

**Solución:**

1. Verifica que tu token esté configurado en `local.settings.json`
2. El token debe tener scope `repo`
3. Con token autenticado tienes 5,000 requests/hora (vs 60 sin token)

### Error: "Function host stopped"

**Síntomas:**
Functions se detiene inesperadamente.

**Solución:**

1. Verifica que tienes .NET 8 SDK:
   ```powershell
   dotnet --list-sdks
   # Debe mostrar: 8.x.x
   ```

2. Limpia y reconstruye:
   ```powershell
   dotnet clean
   dotnet build
   ```

3. Inicia con más logs:
   ```powershell
   func start --verbose
   ```

### No se crean nodos en Neo4j

**Posibles causas:**

1. **El repositorio no tiene archivos .cs**
   - Verifica: el repo debe contener archivos C#
   - Solución: Usa un repo con código .NET

2. **MockEmbeddingService está generando embeddings vacíos**
   - Esto es esperado si no configuras Azure OpenAI
   - Los nodos se crean igual, pero los embeddings son mock

3. **Error en Roslyn Analyzer**
   - Revisa los logs de Functions
   - Busca excepciones con "RoslynAnalyzer"

---

## 🎯 Repositorios de Prueba Recomendados

### Pequeños (para prueba rápida)

```powershell
# Hello World de GitHub (muy pequeño)
$body = @{ owner="octocat"; repo="Hello-World" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" -Method POST -Body $body -ContentType "application/json"

# Tutorial básico de .NET
$body = @{ owner="dotnet"; repo="samples" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" -Method POST -Body $body -ContentType "application/json"
```

### Medianos (para prueba real)

```powershell
# Tu propio proyecto CodeIntel
$body = @{ owner="jesusinfantes-hkteck"; repo="CodeIntel" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" -Method POST -Body $body -ContentType "application/json"

# Proyecto .NET popular
$body = @{ owner="davidfowl"; repo="TodoApi" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" -Method POST -Body $body -ContentType "application/json"
```

---

## 📈 Métricas de Éxito

Al finalizar la ingesta, deberías ver:

✅ **En Functions Logs:**
- "Ingestion completed successfully"
- Tiempo de ejecución (depende del tamaño del repo)
- Número de archivos analizados
- Número de documentos vectoriales creados

✅ **En Neo4j:**
- Nodos `Repository`, `Version`, `Class`, `Method`, `CodeNode` creados
- Relaciones `HAS_CLASS`, `HAS_METHOD`, `CALLS`, `DEPENDS_ON`, `REPRESENTS`
- Índice vectorial `code_embeddings` en estado ONLINE
- Propiedad `embedding` en nodos `CodeNode`

✅ **Queries de Verificación:**
```cypher
// Debe devolver > 0 para cada tipo de nodo
MATCH (n) RETURN labels(n), count(n);

// Debe devolver > 0 relaciones
MATCH ()-[r]->() RETURN type(r), count(r);

// Debe mostrar el índice vectorial
SHOW INDEXES YIELD name, type WHERE type = 'VECTOR';
```

---

## 🎉 Siguiente Paso

Una vez que verifiques que la ingesta funciona y los nodos se crean en Neo4j, puedes:

1. **Probar búsquedas semánticas** (requiere Azure OpenAI para embeddings reales)
2. **Probar GraphRAG** con queries Cypher
3. **Probar el historial de versiones** con los endpoints de webhook
4. **Desplegar a Azure** para producción

---

## 📚 Referencias

- [Neo4j Browser](https://console.neo4j.io)
- [Azure Functions Local Development](https://learn.microsoft.com/en-us/azure/azure-functions/functions-develop-local)
- [GitHub API Rate Limits](https://docs.github.com/en/rest/overview/rate-limits-for-the-rest-api)

---

## ✅ Checklist de Prueba

- [ ] `func start` ejecutándose sin errores
- [ ] Llamar a `/api/ingest` con un repositorio de prueba
- [ ] Ver logs de progreso en la terminal
- [ ] Ver "Ingestion completed successfully"
- [ ] Abrir Neo4j Browser
- [ ] Ejecutar `MATCH (n) RETURN count(n)` y ver > 0 nodos
- [ ] Ejecutar `SHOW INDEXES` y ver `code_embeddings`
- [ ] Ver el grafo visualmente con `MATCH (r:Repository)-[*1..2]-(n) RETURN r, n LIMIT 50`

¡Listo para probar! 🚀
