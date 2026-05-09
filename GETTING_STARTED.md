# 🚀 Guía de Inicio Rápido - CodeIntel

Esta guía te llevará de 0 a tener CodeIntel funcionando con versionado temporal en **menos de 15 minutos**.

---

## 📋 Pre-requisitos Mínimos

- ✅ Windows 10/11
- ✅ PowerShell 5.1+
- ✅ .NET 8 SDK
- ✅ Docker Desktop (o Neo4j Desktop)
- ✅ 8 GB RAM disponible
- ✅ 5 GB espacio en disco

---

## ⚡ Opción 1: Setup Automático (Recomendado)

### Paso 1: Ejecutar script de setup

```powershell
# Abrir PowerShell como Administrador
cd C:\proyectos\gh-code-intel-mvp\src

# Ejecutar setup
.\scripts\Setup-CodeIntel.ps1

# El script hará:
# ✅ Verificar prerequisitos
# ✅ Instalar dependencias faltantes
# ✅ Iniciar Neo4j en Docker
# ✅ Crear índices en Neo4j
# ✅ Configurar appsettings.json
# ✅ Compilar proyecto
```

### Paso 2: Iniciar Functions

```powershell
cd CodeIntel.Functions
func start
```

Deberías ver:

```
Azure Functions Core Tools
Core Tools Version: 4.x.xxxx
Function Runtime Version: 4.x.x.x

Functions:
  GitHubWebhook: [POST] http://localhost:7071/api/webhook/github
  GetVersionHistory: [GET] http://localhost:7071/api/repo/{owner}/{repo}/{branch}/versions
  ...

Host initialized (XXXms)
```

### Paso 3: Probar

```powershell
# En otra ventana de PowerShell
.\scripts\Test-Strategy1.ps1
```

✅ **¡Listo!** CodeIntel está funcionando con versionado temporal Neo4j.

---

## 🔧 Opción 2: Setup Manual

### 1️⃣ Instalar .NET 8

```powershell
# Verificar si ya está instalado
dotnet --version

# Si no está o es versión antigua
winget install Microsoft.DotNet.SDK.8
```

### 2️⃣ Instalar Azure Functions Core Tools

```powershell
npm install -g azure-functions-core-tools@4 --unsafe-perm true

# O con Chocolatey
choco install azure-functions-core-tools-4
```

### 3️⃣ Configurar Neo4j

#### Opción A: Neo4j AuraDB Cloud ⭐ (Recomendado)

**Ventajas:** Sin instalación, 14 días gratis, fully managed, acceso desde cualquier lugar.

1. Ir a: https://console.neo4j.io/
2. Crear cuenta o hacer login
3. Crear nueva instancia **AuraDB Free**
4. ⚠️ **IMPORTANTE:** Copiar el password (solo se muestra una vez)
5. Copiar el Connection URI (ej: `neo4j+s://abc12345.databases.neo4j.io`)

📖 **Guía detallada:** Ver [CONFIGURACION_NEO4J_AURADB.md](CONFIGURACION_NEO4J_AURADB.md)

#### Opción B: Docker (Local)

```powershell
docker run -d `
  --name neo4j-codeintel `
  -p 7474:7474 -p 7687:7687 `
  -e NEO4J_AUTH=neo4j/codeintel123 `
  -e NEO4J_ACCEPT_LICENSE_AGREEMENT=yes `
  neo4j:5-community
```

#### Opción C: Neo4j Desktop (Local)

1. Descargar de https://neo4j.com/download/
2. Instalar y crear nueva base de datos
3. Iniciar database
4. Anotar credenciales

### 4️⃣ Inicializar Schema de Neo4j

#### Si usas AuraDB Cloud:

```powershell
cd C:\proyectos\gh-code-intel-mvp\src\scripts
.\Initialize-Neo4j-Versioned.ps1 `
  -Uri "neo4j+s://TU-INSTANCIA.databases.neo4j.io" `
  -User "neo4j" `
  -Password "TU-PASSWORD-DE-AURADB"
```

#### Si usas Neo4j Local (Docker/Desktop):

```powershell
cd C:\proyectos\gh-code-intel-mvp\src\scripts
.\Initialize-Neo4j-Versioned.ps1 -Password codeintel123
```

O manualmente en Neo4j Browser:
- **AuraDB:** https://console.neo4j.io/ → Tu instancia → "Query"
- **Local:** http://localhost:7474

```cypher
// Constraints
CREATE CONSTRAINT repo_id IF NOT EXISTS 
FOR (r:Repository) REQUIRE r.id IS UNIQUE;

CREATE CONSTRAINT version_id IF NOT EXISTS 
FOR (v:Version) REQUIRE v.id IS UNIQUE;

// Índices temporales (CRÍTICOS)
CREATE INDEX class_temporal IF NOT EXISTS 
FOR (c:Class) ON (c.validFrom, c.validTo);

CREATE INDEX method_temporal IF NOT EXISTS 
FOR (m:Method) ON (m.validFrom, m.validTo);

CREATE INDEX class_version_id IF NOT EXISTS 
FOR (c:Class) ON (c.versionId);

CREATE INDEX method_version_id IF NOT EXISTS 
FOR (m:Method) ON (m.versionId);

CREATE INDEX version_current IF NOT EXISTS 
FOR (v:Version) ON (v.isCurrent);

// Verificar
SHOW INDEXES;
```

### 5️⃣ Configurar GitHub Token

1. Ir a: https://github.com/settings/tokens
2. "Generate new token (classic)"
3. Permisos: `repo` (full control)
4. Copiar el token

Editar `CodeIntel.Functions/appsettings.Development.json`:

**Si usas AuraDB Cloud:**
```json
{
  "GitHub": {
    "Token": "ghp_TU_TOKEN_AQUI"
  },
  "GraphStore": {
    "Type": "Neo4jVersioned"
  },
  "Neo4j": {
    "Uri": "neo4j+s://abc12345.databases.neo4j.io",  // ← Tu URI de AuraDB
    "User": "neo4j",
    "Password": "TuPasswordDeAuraDB"  // ← El que copiaste al crear la instancia
  }
}
```

**Si usas Neo4j Local:**
```json
{
  "GitHub": {
    "Token": "ghp_TU_TOKEN_AQUI"
  },
  "GraphStore": {
    "Type": "Neo4jVersioned"
  },
  "Neo4j": {
    "Uri": "bolt://localhost:7687",
    "User": "neo4j",
    "Password": "codeintel123"
  }
}
```

### 6️⃣ Compilar y Ejecutar

```powershell
cd C:\proyectos\gh-code-intel-mvp\src

# Restaurar paquetes
dotnet restore

# Compilar
dotnet build

# Ejecutar
cd CodeIntel.Functions
func start
```

---

## 🧪 Verificación

### Test 1: Analizar un repositorio

```powershell
# Opción A: Con Invoke-RestMethod (PowerShell)
$body = @{
    owner = "octocat"
    repo = "Hello-World"
    branch = "master"
} | ConvertTo-Json

Invoke-RestMethod `
    -Uri http://localhost:7071/api/ingest `
    -Method POST `
    -Body $body `
    -ContentType "application/json"
```

```bash
# Opción B: Con curl
curl -X POST http://localhost:7071/api/ingest \
  -H "Content-Type: application/json" \
  -d '{
    "owner": "octocat",
    "repo": "Hello-World",
    "branch": "master"
  }'
```

**Respuesta esperada:**

```json
{
  "repo": "octocat/Hello-World@master",
  "downloadedTo": "C:\\Users\\...\\Temp\\codeintel\\...",
  "classes": 2,
  "methods": 8,
  "edges": 15,
  "indexed": 10
}
```

### Test 2: Ver versiones

```powershell
Invoke-RestMethod -Uri http://localhost:7071/api/repo/octocat/Hello-World/master/versions
```

**Respuesta esperada:**

```json
{
  "repoId": "octocat/Hello-World@master",
  "totalVersions": 1,
  "versions": [
    {
      "versionId": "abc123...",
      "commitHash": "unknown",
      "timestamp": "2024-01-15T10:30:00Z",
      "isCurrent": true,
      "age": "00:00:05:23"
    }
  ]
}
```

### Test 3: Neo4j Browser

1. Abrir: http://localhost:7474
2. Login: `neo4j` / `codeintel123`
3. Ejecutar:

```cypher
// Ver repositorios
MATCH (r:Repository)
RETURN r

// Ver versiones
MATCH (v:Version)
RETURN v.id, v.timestamp, v.isCurrent

// Ver clases
MATCH (c:Class)
RETURN c.name, c.namespace, c.validFrom, c.validTo
LIMIT 10
```

---

## 🎯 Primer Flujo Completo

### Escenario: Trackear cambios en un repositorio

#### 1. Analizar versión inicial

```powershell
$repo = @{
    owner = "tu-usuario"
    repo = "tu-repositorio"
    branch = "main"
} | ConvertTo-Json

Invoke-RestMethod -Uri http://localhost:7071/api/ingest `
    -Method POST -Body $repo -ContentType "application/json"
```

#### 2. Ver versiones

```powershell
$versions = Invoke-RestMethod `
    -Uri http://localhost:7071/api/repo/tu-usuario/tu-repositorio/main/versions

$versions.versions | Format-Table
```

#### 3. Simular cambio (hacer commit en GitHub o re-analizar)

```powershell
# Esperar un momento
Start-Sleep -Seconds 10

# Re-analizar (simula nuevo commit)
Invoke-RestMethod -Uri http://localhost:7071/api/ingest `
    -Method POST -Body $repo -ContentType "application/json"
```

#### 4. Verificar nueva versión

```powershell
$versionsUpdated = Invoke-RestMethod `
    -Uri http://localhost:7071/api/repo/tu-usuario/tu-repositorio/main/versions

Write-Host "Total versiones: $($versionsUpdated.totalVersions)"
```

#### 5. Rollback a versión anterior

```powershell
# Obtener ID de versión anterior
$previousVersionId = $versionsUpdated.versions[1].versionId

# Hacer rollback
$rollbackBody = @{ versionId = $previousVersionId } | ConvertTo-Json

Invoke-RestMethod `
    -Uri http://localhost:7071/api/repo/tu-usuario/tu-repositorio/main/rollback `
    -Method POST `
    -Body $rollbackBody `
    -ContentType "application/json"
```

#### 6. Verificar rollback

```powershell
$versionsAfterRollback = Invoke-RestMethod `
    -Uri http://localhost:7071/api/repo/tu-usuario/tu-repositorio/main/versions

$current = $versionsAfterRollback.versions | Where-Object { $_.isCurrent }
Write-Host "Versión actual ahora: $($current.versionId)"
```

---

## 🔍 Queries Útiles de Neo4j

### Ver evolución temporal

```cypher
// Todas las versiones de una clase específica
MATCH path = (c:Class {name: "Program"})-[:NEXT_VERSION*0..]->(latest)
WHERE c.repoId = "owner/repo@main"
RETURN nodes(path)
```

### Comparar dos versiones

```cypher
// Clases agregadas en última versión
MATCH (v1:Version {isCurrent: true})-[:CONTAINS]->(c1:Class)
WHERE NOT EXISTS {
    MATCH (v2:Version)-[:CONTAINS]->(c2:Class)
    WHERE c1.id = c2.id 
      AND v2.timestamp < v1.timestamp
}
RETURN c1.name AS nuevasClases
```

### Ver código en fecha específica

```cypher
// Clases válidas el 15 de enero de 2024
MATCH (c:Class {repoId: "owner/repo@main"})
WHERE c.validFrom <= 1705324800
  AND (c.validTo IS NULL OR c.validTo > 1705324800)
RETURN c.name, c.namespace
```

---

## 🐛 Troubleshooting Común

### ❌ "Failed to connect to Neo4j"

**Solución:**

```powershell
# Verificar que Neo4j esté corriendo
docker ps | Select-String neo4j

# Si no está, iniciarlo
docker start neo4j-codeintel

# Verificar conectividad
Test-NetConnection localhost -Port 7687
```

### ❌ "GitHub:Token missing"

**Solución:**

1. Verificar `appsettings.Development.json`
2. Asegurar que el token sea válido
3. Verificar permisos del token (`repo`)

### ❌ "Package Neo4j.Driver not found"

**Solución:**

```powershell
cd C:\proyectos\gh-code-intel-mvp\src
dotnet restore
dotnet build
```

### ❌ "func: command not found"

**Solución:**

```powershell
# Instalar Azure Functions Core Tools
npm install -g azure-functions-core-tools@4 --unsafe-perm true

# Reiniciar PowerShell
```

### ❌ Queries lentas en Neo4j

**Solución:**

```cypher
// Verificar índices
SHOW INDEXES;

// Si faltan, crearlos
CREATE INDEX class_temporal IF NOT EXISTS 
FOR (c:Class) ON (c.validFrom, c.validTo);
```

---

## 📚 Próximos Pasos

### 1. Configurar Webhooks de GitHub

1. Ir a tu repositorio en GitHub
2. Settings → Webhooks → Add webhook
3. Payload URL: `http://tu-servidor/api/webhook/github`
4. Content type: `application/json`
5. Events: `push`

### 2. Explorar Documentación

- **README.md** - Documentación completa
- **docs/Guia_Uso_Versionado.md** - Ejemplos avanzados
- **docs/Versionado_y_Rollback_Neo4j.md** - Detalles técnicos

### 3. Desplegar a Azure

```powershell
# Login
az login

# Deploy
cd CodeIntel.Functions
func azure functionapp publish tu-function-app
```

---

## 💡 Tips y Trucos

### Ver logs de Neo4j

```powershell
docker logs -f neo4j-codeintel
```

### Limpiar datos de prueba

```cypher
// En Neo4j Browser
MATCH (n)
DETACH DELETE n
```

### Backup de Neo4j

```powershell
docker exec neo4j-codeintel neo4j-admin database dump neo4j --to-path=/tmp
docker cp neo4j-codeintel:/tmp/neo4j.dump ./backup/
```

### Monitorear performance

```cypher
// Ver queries lentas
CALL dbms.listQueries()
```

---

## 🎉 ¡Listo!

Ya tienes CodeIntel funcionando con Estrategia 1 (Versionado Temporal).

**Características disponibles:**
- ✅ Análisis de código C# con Roslyn
- ✅ Versionado temporal completo
- ✅ Rollback a versiones anteriores
- ✅ Consultas temporales
- ✅ Integración GitHub
- ✅ APIs REST

**¿Preguntas?**
- 📧 Abrir issue en GitHub
- 📚 Ver documentación en `/docs`
- 💬 GitHub Discussions

---

*Generado para CodeIntel v1.0.0 - Estrategia 1 (Versionado Temporal)*
