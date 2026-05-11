# 🧪 GUÍA DE TESTING - Versionado Vectorial con Mocks

## 📋 **OBJETIVO**

Testear la implementación del versionado vectorial usando **Mocks** antes de tocar Neo4j.

---

## ⚙️ **CONFIGURACIÓN PARA TESTING CON MOCKS**

### **Opción 1: Usar appsettings.json (Recomendado)**

Abre `AriadnaKnowledgeStore.Functions\appsettings.json` y configura:

```json
{
  "GitHub": {
    "Token": "tu-github-token-aqui"
  },
  "GraphStore": {
    "Type": "Mock"  // ← CAMBIAR A "Mock"
  },
  "AzureOpenAI": {
    // Dejar vacío para usar MockEmbeddingService
  },
  "Neo4j": {
    // No se usará con Mock
  }
}
```

**Resultado:** Usará `MockGraphStore`, `MockVectorIndex` y `MockEmbeddingService`.

---

### **Opción 2: Usar Variables de Entorno**

```powershell
# En PowerShell
$env:GraphStore__Type = "Mock"
$env:GitHub__Token = "tu-github-token"

# Ejecutar Azure Functions
func start
```

---

### **Opción 3: Modificar Temporalmente Program.cs**

Si quieres forzar mocks sin configuración:

```csharp
// Línea 37 - Cambiar a:
var graphStoreType = "Mock"; // Forzar Mock para testing

// Línea 67 - Cambiar a:
bool useRealAzureOpenAI = false; // Forzar Mock embeddings
```

⚠️ **Recuerda revertir estos cambios después del testing.**

---

## 🧪 **PLAN DE TESTING**

### **Test 1: Ingesta Simple (Primera Versión)**

**Objetivo:** Verificar que se crea un `VersionContext` correctamente.

#### **Pasos:**

1. **Configurar Mocks** (según Opción 1, 2 o 3)

2. **Ejecutar Azure Function:**
```powershell
cd C:\proyectos\gh-ariadna-knowledgestore-mvp\src\AriadnaKnowledgeStore.Functions
func start
```

3. **Llamar al endpoint de ingesta:**
```powershell
# POST request
$body = @{
    owner = "test-owner"
    repo = "test-repo"
    branch = "main"
    path = "commit-abc123"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"
```

4. **Verificar Output en Console:**

Busca en la consola de Azure Functions:

```
✅ ESPERADO:
[MOCK] Storing graph: X classes, Y methods, Version: guid-12345...
[MOCK] Indexed Z documents for version guid-12345... (total: Z)
✅ Stored graph with version: guid-12345...
✅ Indexed Z documents with version: guid-12345...
```

5. **Verificar Response JSON:**

```json
{
  "repo": "test-owner/test-repo@main",
  "downloadedTo": "C:\\temp\\...",
  "versionId": "guid-12345...",         // ← NUEVO
  "timestamp": 1704067200,              // ← NUEVO
  "commitHash": "commit-abc123",        // ← NUEVO
  "classes": 5,
  "methods": 20,
  "indexed": 25
}
```

✅ **Si ves `versionId`, `timestamp` y `commitHash` → TEST PASADO**

---

### **Test 2: Ingesta Doble (Múltiples Versiones)**

**Objetivo:** Verificar que cada ingesta crea un nuevo `VersionContext`.

#### **Pasos:**

1. **Primera ingesta:**
```powershell
$body1 = @{
    owner = "test"
    repo = "repo1"
    branch = "main"
    path = "commit-v1"
} | ConvertTo-Json

$response1 = Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" `
    -Method Post -Body $body1 -ContentType "application/json"

$version1 = $response1.versionId
Write-Host "Version 1: $version1"
```

2. **Segunda ingesta (mismo repo):**
```powershell
$body2 = @{
    owner = "test"
    repo = "repo1"
    branch = "main"
    path = "commit-v2"  # ← Diferente commit
} | ConvertTo-Json

$response2 = Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" `
    -Method Post -Body $body2 -ContentType "application/json"

$version2 = $response2.versionId
Write-Host "Version 2: $version2"
```

3. **Verificar en Console:**

```
Primera ingesta:
  [MOCK] Version: guid-aaa...

Segunda ingesta:
  [MOCK] Version: guid-bbb...  ← DIFERENTE
```

4. **Verificar Versiones Diferentes:**
```powershell
if ($version1 -ne $version2) {
    Write-Host "✅ TEST PASADO: Cada ingesta crea una versión única" -ForegroundColor Green
} else {
    Write-Host "❌ TEST FALLIDO: Las versiones son iguales" -ForegroundColor Red
}
```

✅ **Si `versionId` es diferente en cada ingesta → TEST PASADO**

---

### **Test 3: Verificar Flujo Completo**

**Objetivo:** Validar que `VersionContext` fluye correctamente de Graph a Vector.

#### **Logs Esperados:**

```
📁 Downloaded to: C:\temp\...
📊 Analyzed: X classes, Y methods...
[MOCK] Storing graph: X classes, Y methods, Version: guid-123
✅ Stored graph with version: guid-123        ← Graph devuelve version
📦 Generated Z code chunks for embedding
🔢 Generated Z embeddings
[MOCK] Indexed Z documents for version guid-123  ← Vector recibe MISMO version
✅ Indexed Z documents with version: guid-123
```

✅ **Si los logs muestran el MISMO `versionId` en ambos lugares → TEST PASADO**

---

### **Test 4: Verificar Metadata Completa**

**Objetivo:** Validar que `VersionContext` tiene toda la información.

#### **Script de Validación:**

```powershell
$response = Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" `
    -Method Post -Body $body -ContentType "application/json"

# Verificar campos
$requiredFields = @("versionId", "timestamp", "commitHash", "repo")
$allPresent = $true

foreach ($field in $requiredFields) {
    if (-not $response.PSObject.Properties[$field]) {
        Write-Host "❌ Falta campo: $field" -ForegroundColor Red
        $allPresent = $false
    } else {
        Write-Host "✅ Campo presente: $field = $($response.$field)" -ForegroundColor Green
    }
}

if ($allPresent) {
    Write-Host "`n✅ TODOS LOS CAMPOS PRESENTES - TEST PASADO" -ForegroundColor Green
} else {
    Write-Host "`n❌ FALTAN CAMPOS - TEST FALLIDO" -ForegroundColor Red
}
```

---

## 📊 **CHECKLIST DE VALIDACIÓN**

Marca cada item cuando lo verifiques:

```
□ appsettings.json configurado con GraphStore:Type = "Mock"
□ Azure Function inicia sin errores
□ Endpoint /ingest responde correctamente
□ Response JSON incluye "versionId"
□ Response JSON incluye "timestamp"
□ Response JSON incluye "commitHash"
□ Logs muestran "[MOCK] Version: guid-..."
□ MockGraphStore devuelve VersionContext
□ MockVectorIndex recibe VersionContext
□ VersionId es IGUAL entre Graph y Vector
□ Cada ingesta genera VersionId DIFERENTE
□ No hay errores de compilación
□ No hay excepciones en runtime
```

---

## 🐛 **TROUBLESHOOTING**

### **Error: "GitHub:Token missing"**
```
Solución: Agregar token en appsettings.json:
{
  "GitHub": {
    "Token": "ghp_tu_token_aqui"
  }
}
```

### **Error: "Neo4j:Password missing"**
```
Solución: Cambiar a Mock en appsettings.json:
{
  "GraphStore": {
    "Type": "Mock"
  }
}
```

### **No se ven logs de Mock**
```
Solución: Verificar LogLevel en appsettings.json:
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### **Response no incluye versionId**
```
Posible causa: Cambios no se aplicaron correctamente
Solución:
1. Detener Azure Function (Ctrl+C)
2. Limpiar build: dotnet clean
3. Recompilar: dotnet build
4. Reiniciar: func start
```

---

## 📝 **SCRIPT COMPLETO DE TESTING**

Guarda este script como `test-versioning.ps1`:

```powershell
# test-versioning.ps1
Write-Host "🧪 Testing Versionado Vectorial con Mocks" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Test 1: Primera Ingesta
Write-Host "Test 1: Primera Ingesta..." -ForegroundColor Yellow
$body1 = @{
    owner = "test"
    repo = "sample"
    branch = "main"
    path = "v1"
} | ConvertTo-Json

try {
    $response1 = Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" `
        -Method Post -Body $body1 -ContentType "application/json"

    if ($response1.versionId) {
        Write-Host "✅ Primera ingesta OK - Version: $($response1.versionId)" -ForegroundColor Green
        $version1 = $response1.versionId
    } else {
        Write-Host "❌ Falta versionId en respuesta" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error en primera ingesta: $_" -ForegroundColor Red
    exit 1
}

Start-Sleep -Seconds 2

# Test 2: Segunda Ingesta
Write-Host "`nTest 2: Segunda Ingesta..." -ForegroundColor Yellow
$body2 = @{
    owner = "test"
    repo = "sample"
    branch = "main"
    path = "v2"
} | ConvertTo-Json

try {
    $response2 = Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" `
        -Method Post -Body $body2 -ContentType "application/json"

    if ($response2.versionId) {
        Write-Host "✅ Segunda ingesta OK - Version: $($response2.versionId)" -ForegroundColor Green
        $version2 = $response2.versionId
    } else {
        Write-Host "❌ Falta versionId en respuesta" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error en segunda ingesta: $_" -ForegroundColor Red
    exit 1
}

# Test 3: Verificar Versiones Diferentes
Write-Host "`nTest 3: Verificando versiones únicas..." -ForegroundColor Yellow
if ($version1 -ne $version2) {
    Write-Host "✅ Las versiones son diferentes" -ForegroundColor Green
    Write-Host "   V1: $version1" -ForegroundColor Gray
    Write-Host "   V2: $version2" -ForegroundColor Gray
} else {
    Write-Host "❌ Las versiones son iguales (ERROR)" -ForegroundColor Red
    exit 1
}

# Test 4: Verificar Campos Requeridos
Write-Host "`nTest 4: Verificando campos requeridos..." -ForegroundColor Yellow
$requiredFields = @("versionId", "timestamp", "commitHash", "repo", "classes", "methods", "indexed")
$allPresent = $true

foreach ($field in $requiredFields) {
    if ($response2.PSObject.Properties[$field]) {
        Write-Host "✅ $field : $($response2.$field)" -ForegroundColor Green
    } else {
        Write-Host "❌ Falta campo: $field" -ForegroundColor Red
        $allPresent = $false
    }
}

# Resultado Final
Write-Host "`n========================================" -ForegroundColor Cyan
if ($allPresent) {
    Write-Host "✅ TODOS LOS TESTS PASADOS" -ForegroundColor Green
    Write-Host "Sistema listo para probar con Neo4j" -ForegroundColor Green
} else {
    Write-Host "❌ ALGUNOS TESTS FALLARON" -ForegroundColor Red
    Write-Host "Revisar implementación" -ForegroundColor Red
    exit 1
}
```

#### **Ejecutar Script:**
```powershell
# 1. Iniciar Azure Function en una terminal
cd C:\proyectos\gh-ariadna-knowledgestore-mvp\src\AriadnaKnowledgeStore.Functions
func start

# 2. En OTRA terminal, ejecutar el script de testing
.\test-versioning.ps1
```

---

## ✅ **CRITERIOS DE ÉXITO**

El testing con Mocks es exitoso si:

1. ✅ Todas las ingestas devuelven `versionId` único
2. ✅ Logs muestran "[MOCK] Version: ..." en Graph y Vector
3. ✅ El mismo `versionId` aparece en Graph y Vector
4. ✅ Response JSON incluye todos los campos nuevos
5. ✅ No hay errores ni excepciones
6. ✅ Script de testing pasa todos los tests

---

## 🚀 **DESPUÉS DEL TESTING CON MOCKS**

Una vez que TODOS los tests pasen con Mocks:

1. **Cambiar a Neo4j:**
```json
{
  "GraphStore": {
    "Type": "Neo4jVersioned"
  },
  "Neo4j": {
    "Uri": "bolt://localhost:7687",
    "User": "neo4j",
    "Password": "tu-password"
  }
}
```

2. **Limpiar Neo4j (opcional):**
```cypher
MATCH (n) DETACH DELETE n
```

3. **Ejecutar ingesta real:**
```powershell
# Misma llamada que antes, pero ahora guardará en Neo4j
Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" ...
```

4. **Verificar en Neo4j Browser:**
```cypher
// Ver versiones
MATCH (v:Version) RETURN v

// Ver CodeNodes versionados
MATCH (n:CodeNode) 
RETURN n.id, n.versionId, n.validFrom, n.validTo
LIMIT 10
```

---

## 📚 **SIGUIENTES PASOS**

Una vez validado con Mocks Y Neo4j:

1. Testing de Time Travel (búsqueda histórica)
2. Testing de Rollback
3. Testing de Múltiples Repositorios
4. Performance testing

---

**Creado:** 2024  
**Propósito:** Validar versionado vectorial sin riesgo  
**Estado:** Listo para ejecutar
