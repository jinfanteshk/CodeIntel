# CodeIntel - Knowledge Store con Versionado Temporal

Sistema de análisis y gestión de conocimiento de código con soporte completo de versionado y rollback usando Neo4j.

## 🌟 Características Principales

- ✅ **Versionado Temporal (Bitemporal)**: Historial completo de cambios con capacidad de rollback
- ✅ **Análisis de Código**: Extracción automática de clases, métodos y dependencias usando Roslyn
- ✅ **Knowledge Graph**: Navegación y consulta de relaciones de código en Neo4j
- ✅ **Vector Search**: Búsqueda semántica con embeddings de Azure OpenAI
- ✅ **Integración GitHub**: Webhooks para actualización automática en cada commit
- ✅ **Auditoría Temporal**: Consulta el estado del código en cualquier punto del tiempo
- ✅ **Azure Functions**: Despliegue serverless con escalado automático

---

## 🚀 Quick Start

### Prerequisitos

1. **.NET 8 SDK**
   ```powershell
   winget install Microsoft.DotNet.SDK.8
   ```

2. **Neo4j Desktop o Neo4j Server** (versión 5.x recomendada)
   - Descarga: https://neo4j.com/download/
   - O usando Docker:
     ```powershell
     docker run -d `
       --name neo4j `
       -p 7474:7474 -p 7687:7687 `
       -e NEO4J_AUTH=neo4j/your-password `
       neo4j:5-enterprise
     ```

3. **Azure OpenAI** (opcional, para embeddings)
   - O usa el mock integrado para desarrollo

### Instalación

1. **Clonar repositorio**
   ```powershell
   cd C:\proyectos
   git clone https://github.com/jinfanteshk/CodeIntel
   cd CodeIntel\src
   ```

2. **Restaurar paquetes**
   ```powershell
   dotnet restore
   ```

3. **Configurar appsettings.json**
   ```powershell
   cd CodeIntel.Functions
   copy appsettings.json appsettings.Development.json
   notepad appsettings.Development.json
   ```

   **Opción A: Neo4j AuraDB Cloud ⭐ (Recomendado)**
   ```json
   {
     "GitHub": {
       "Token": "ghp_TU_GITHUB_TOKEN_AQUI"
     },
     "GraphStore": {
       "Type": "Neo4jVersioned"
     },
     "Neo4j": {
       "Uri": "neo4j+s://abc12345.databases.neo4j.io",  // Tu URI de AuraDB
       "User": "neo4j",
       "Password": "tu-password-de-auradb"
     }
   }
   ```

   **Opción B: Neo4j Local (Docker/Desktop)**
   ```json
   {
     "GitHub": {
       "Token": "ghp_TU_GITHUB_TOKEN_AQUI"
     },
     "GraphStore": {
       "Type": "Neo4jVersioned"
     },
     "Neo4j": {
       "Uri": "bolt://localhost:7687",
       "User": "neo4j",
       "Password": "tu-password-local"
     }
   }
   ```

   📖 **Guía detallada:** [CONFIGURACION_NEO4J_AURADB.md](CONFIGURACION_NEO4J_AURADB.md)

4. **Inicializar Neo4j**

   **Para AuraDB:**
   ```powershell
   cd ..\scripts
   .\Initialize-Neo4j-Versioned.ps1 `
     -Uri "neo4j+s://tu-instancia.databases.neo4j.io" `
     -User "neo4j" `
     -Password "tu-password-de-auradb"
   ```

   **Para Neo4j Local:**
   ```powershell
   cd ..\scripts
   .\Initialize-Neo4j-Versioned.ps1 -Password "tu-password-local"
   ```

   Esto creará:
   - ✅ Constraints de unicidad
   - ✅ Índices temporales para performance
   - ✅ Índices de búsqueda

5. **Ejecutar localmente**
   ```powershell
   cd ..\CodeIntel.Functions
   func start
   ```

   Deberías ver:
   ```
   Azure Functions Core Tools
   Core Tools Version: 4.x
   Function Runtime Version: 4.x

   Functions:
   - GitHubWebhook: [POST] http://localhost:7071/api/webhook/github
   - GetVersionHistory: [GET] http://localhost:7071/api/repo/{owner}/{repo}/{branch}/versions
   - RollbackToVersion: [POST] http://localhost:7071/api/repo/{owner}/{repo}/{branch}/rollback
   - GetGraphAtTime: [GET] http://localhost:7071/api/repo/{owner}/{repo}/{branch}/snapshot
   ```

---

## 📖 Uso

### 1. Analizar un repositorio manualmente

```powershell
# Usar la Function existente o crear una request HTTP
curl -X POST http://localhost:7071/api/ingest `
  -H "Content-Type: application/json" `
  -d '{
    "owner": "tu-usuario",
    "repo": "tu-repositorio",
    "branch": "main"
  }'
```

### 2. Ver historial de versiones

```powershell
curl http://localhost:7071/api/repo/tu-usuario/tu-repositorio/main/versions
```

**Respuesta:**
```json
{
  "repoId": "tu-usuario/tu-repositorio@main",
  "totalVersions": 3,
  "versions": [
    {
      "versionId": "a1b2c3d4",
      "commitHash": "7f8e9a1b",
      "timestamp": "2024-01-15T10:30:00Z",
      "isCurrent": true,
      "age": "00:00:15:23"
    },
    {
      "versionId": "e5f6g7h8",
      "commitHash": "6d5c4b3a",
      "timestamp": "2024-01-14T15:20:00Z",
      "isCurrent": false,
      "age": "1.19:10:00"
    }
  ]
}
```

### 3. Hacer rollback a una versión anterior

```powershell
curl -X POST http://localhost:7071/api/repo/tu-usuario/tu-repositorio/main/rollback `
  -H "Content-Type: application/json" `
  -d '{"versionId": "e5f6g7h8"}'
```

### 4. Consultar estado histórico

```powershell
# Ver el código como estaba el 15 de enero de 2024 a las 12:00 UTC
$timestamp = [DateTimeOffset]::Parse("2024-01-15T12:00:00Z").ToUnixTimeSeconds()
curl "http://localhost:7071/api/repo/tu-usuario/tu-repositorio/main/snapshot?timestamp=$timestamp"
```

---

## 🔧 Configuración Avanzada

### Configuración de Storage

El sistema utiliza Neo4j para almacenamiento de grafo y vectores (configurar en `appsettings.json`):

```json
{
  "GraphStore": {
    "Type": "Neo4jVersioned"  // ← Recomendado para producción
  }
}
```

| Opción | Valor Config | Descripción |
|--------|-------------|-------------|
| **Neo4j Versionado** ✅ | `Neo4jVersioned` | Versionado bitemporal, historial completo, queries temporales, rollback |
| **Mock (Testing)** | `Mock` | Store en memoria para desarrollo y pruebas unitarias |

### Políticas de Retención

```json
{
  "VersionManagement": {
    "RetentionDays": 90,
    "AutoCleanup": false
  }
}
```

Habilitar limpieza automática con timer trigger:

```csharp
[Function("CleanupOldVersions")]
public async Task CleanupScheduled(
    [TimerTrigger("0 0 2 * * *")] TimerInfo timer) // 2 AM diario
{
    var retentionDays = int.Parse(_config["VersionManagement:RetentionDays"] ?? "90");
    await CleanupOldVersionsAsync(retentionDays);
}
```

---

## 🔍 Queries Útiles de Neo4j

### Ver todas las versiones de un repo

```cypher
MATCH (r:Repository {id: "owner/repo@main"})-[:HAS_VERSION]->(v:Version)
RETURN v.id AS versionId, 
       v.commitHash AS commit,
       datetime({epochSeconds: v.timestamp}) AS timestamp,
       v.isCurrent AS current
ORDER BY v.timestamp DESC
```

### Clases válidas en un timestamp específico

```cypher
// Código como estaba el 15 de enero de 2024
MATCH (c:Class {repoId: "owner/repo@main"})
WHERE c.validFrom <= 1705324800 
  AND (c.validTo IS NULL OR c.validTo > 1705324800)
RETURN c.name, c.namespace, c.filePath
```

### Comparar dos versiones (clases nuevas)

```cypher
// Clases agregadas en versión 2
MATCH (v1:Version {id: $version1})-[:CONTAINS]->(c1:Class)
WHERE NOT EXISTS {
    MATCH (v2:Version {id: $version2})-[:CONTAINS]->(c2:Class)
    WHERE c1.id = c2.id
}
RETURN c1.name, c1.namespace
```

### Ver evolución de una clase

```cypher
MATCH path = (c:Class {id: "class:MyNamespace.MyClass"})-[:NEXT_VERSION*0..]->(latest:Class)
WHERE c.versionId = $initialVersion
RETURN nodes(path) AS versions
```

---

## 📊 Arquitectura

```
┌─────────────────────────────────────────────────────────────┐
│                    GitHub Repository                         │
│                    (Source of Truth)                         │
└─────────────────┬───────────────────────────────────────────┘
                  │ Push/PR
                  │ Webhook
                  ▼
┌─────────────────────────────────────────────────────────────┐
│             Azure Functions (CodeIntel.Functions)            │
│  ┌──────────────────┐  ┌────────────────────────────────┐  │
│  │ GitHubWebhook    │  │ Version Management             │  │
│  │ - HandleWebhook  │  │ - GetVersionHistory            │  │
│  │ - Auto-trigger   │  │ - RollbackToVersion            │  │
│  └──────────────────┘  │ - GetGraphAtTime               │  │
│                        └────────────────────────────────┘  │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ├──────────────┬──────────────┬──────────────┐
                  ▼              ▼              ▼              ▼
        ┌─────────────┐  ┌─────────────┐  ┌──────────┐  ┌────────────┐
        │   Roslyn    │  │   Neo4j     │  │  Azure   │  │   Azure    │
        │  Analyzer   │  │  Versioned  │  │  OpenAI  │  │   Search   │
        │             │  │   Graph     │  │Embeddings│  │   Vector   │
        │ (Extraer    │  │             │  │          │  │   Index    │
        │  clases,    │  │ (Versioning │  │ (Generar │  │            │
        │  métodos,   │  │  temporal)  │  │ vectors) │  │ (Búsqueda  │
        │  relaciones)│  │             │  │          │  │ semántica) │
        └─────────────┘  └─────────────┘  └──────────┘  └────────────┘
```

### Componentes

| Proyecto | Descripción |
|----------|-------------|
| **CodeIntel.Core** | Modelos, interfaces, abstracciones |
| **CodeIntel.Ingest** | Roslyn analyzer, GitHub source, chunking |
| **CodeIntel.Graph** | Neo4j stores (versioned, multi-DB, legacy) |
| **CodeIntel.Vector** | Azure OpenAI embeddings, Azure Search |
| **CodeIntel.Functions** | Azure Functions, webhooks, orquestación |

---

## 🔐 Seguridad

### Configuración de Webhooks GitHub

1. **Generar secret para validar firma**
   ```powershell
   $secret = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes([Guid]::NewGuid().ToString()))
   Write-Host "GitHub Webhook Secret: $secret"
   ```

2. **Configurar en GitHub**
   - Ir a: Settings → Webhooks → Add webhook
   - Payload URL: `https://your-app.azurewebsites.net/api/webhook/github?code=YOUR_FUNCTION_KEY`
   - Content type: `application/json`
   - Secret: (el generado arriba)
   - Events: `push`, `pull_request`

3. **Validar firma en código** (ya implementado en `GitHubWebhookFunction.cs`)

### Azure Key Vault (Producción)

```json
{
  "GitHub": {
    "Token": "@Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/GitHubToken/)"
  },
  "Neo4j": {
    "Password": "@Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/Neo4jPassword/)"
  }
}
```

---

## 🧪 Testing

### Ejecutar tests

```powershell
dotnet test
```

### Test manual con PowerShell

```powershell
# Script de prueba completo
$baseUrl = "http://localhost:7071"

# 1. Analizar repo
$analyzeResponse = Invoke-RestMethod `
  -Uri "$baseUrl/api/ingest" `
  -Method POST `
  -Body (@{
    owner = "microsoft"
    repo = "dotnet"
    branch = "main"
  } | ConvertTo-Json) `
  -ContentType "application/json"

Write-Host "✅ Análisis completado: $($analyzeResponse | ConvertTo-Json)"

# 2. Ver versiones
$versions = Invoke-RestMethod `
  -Uri "$baseUrl/api/repo/microsoft/dotnet/main/versions"

Write-Host "📋 Versiones:"
$versions.versions | Format-Table

# 3. Rollback (si hay más de 1 versión)
if ($versions.totalVersions -gt 1) {
    $previousVersion = $versions.versions[1].versionId

    $rollbackResponse = Invoke-RestMethod `
      -Uri "$baseUrl/api/repo/microsoft/dotnet/main/rollback" `
      -Method POST `
      -Body (@{ versionId = $previousVersion } | ConvertTo-Json) `
      -ContentType "application/json"

    Write-Host "⏮️  Rollback completado: $($rollbackResponse | ConvertTo-Json)"
}
```

---

## 📚 Documentación Adicional

- [Guía de Versionado y Rollback](../docs/Guia_Uso_Versionado.md)
- [Estrategias de Versionado Comparadas](../docs/Versionado_y_Rollback_Neo4j.md)
- [Discurso de Presentación](../Discurso_CodeIntel_Presentacion.md)

---

## 🚀 Despliegue a Azure

### 1. Crear recursos

```powershell
# Variables
$resourceGroup = "rg-codeintel"
$location = "eastus"
$storageAccount = "stcodeintel"
$functionApp = "func-codeintel"
$neo4jVm = "vm-neo4j"

# Crear resource group
az group create --name $resourceGroup --location $location

# Crear storage account
az storage account create `
  --name $storageAccount `
  --resource-group $resourceGroup `
  --location $location `
  --sku Standard_LRS

# Crear function app
az functionapp create `
  --name $functionApp `
  --resource-group $resourceGroup `
  --storage-account $storageAccount `
  --consumption-plan-location $location `
  --runtime dotnet-isolated `
  --runtime-version 8 `
  --functions-version 4

# Neo4j en Azure VM o usar Neo4j AuraDB (managed)
```

### 2. Desplegar código

```powershell
cd C:\proyectos\gh-code-intel-mvp\src\CodeIntel.Functions

# Publicar
func azure functionapp publish func-codeintel
```

### 3. Configurar Application Settings

```powershell
az functionapp config appsettings set `
  --name $functionApp `
  --resource-group $resourceGroup `
  --settings `
    "GraphStore__Type=Neo4jVersioned" `
    "Neo4j__Uri=bolt://your-neo4j-server:7687" `
    "Neo4j__User=neo4j" `
    "Neo4j__Password=@Microsoft.KeyVault(...)"
```

---

## 🐛 Troubleshooting

### "GitHub:Token missing"

**Solución:** Configura tu GitHub Personal Access Token en `appsettings.json`

```json
{
  "GitHub": {
    "Token": "ghp_..."
  }
}
```

### "Failed to connect to Neo4j"

**Verificar:**
1. Neo4j está corriendo: `docker ps` o Neo4j Desktop
2. Puerto 7687 está abierto: `Test-NetConnection localhost -Port 7687`
3. Credenciales correctas en config

### "Package Neo4j.Driver not found"

```powershell
cd CodeIntel.Graph
dotnet add package Neo4j.Driver
cd ..\CodeIntel.Functions
dotnet restore
```

### Queries lentas en versiones antiguas

**Solución:** Verificar índices

```cypher
SHOW INDEXES;

// Si faltan índices temporales:
CREATE INDEX class_temporal IF NOT EXISTS
FOR (c:Class) ON (c.validFrom, c.validTo);
```

---

## 🤝 Contribuir

1. Fork el repositorio
2. Crea una rama feature: `git checkout -b feature/mi-feature`
3. Commit tus cambios: `git commit -am 'Add mi feature'`
4. Push a la rama: `git push origin feature/mi-feature`
5. Abre un Pull Request

---

## 📄 Licencia

MIT License - ver [LICENSE](LICENSE) para detalles

---

## 👥 Autores

- **Equipo CodeIntel** - [GitHub](https://github.com/jinfanteshk/CodeIntel)

---

## 🙏 Agradecimientos

- [Neo4j](https://neo4j.com/) - Knowledge Graph Database
- [Roslyn](https://github.com/dotnet/roslyn) - .NET Compiler Platform
- [Azure Functions](https://azure.microsoft.com/services/functions/) - Serverless Compute
- [Azure OpenAI](https://azure.microsoft.com/products/ai-services/openai-service/) - Embeddings & AI
