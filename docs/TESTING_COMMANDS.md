# 🔧 COMANDOS ÚTILES - Testing & Debugging

## 📦 Testing Individual

### **Single Ingest Request**

```powershell
# Request básico
$body = @{
    owner = "test"
    repo = "sample"
    branch = "main"
    path = "abc123"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" `
    -Method Post -Body $body -ContentType "application/json"

# Ver response completo
$response | ConvertTo-Json -Depth 10

# Ver solo versionId
Write-Host "VersionId: $($response.versionId)" -ForegroundColor Cyan
```

---

### **Verificar Campo Específico**

```powershell
# Verificar versionId
if ($response.versionId) {
    Write-Host "✅ versionId presente: $($response.versionId)" -ForegroundColor Green
} else {
    Write-Host "❌ versionId ausente" -ForegroundColor Red
}

# Verificar timestamp
if ($response.timestamp) {
    $date = [DateTimeOffset]::FromUnixTimeSeconds($response.timestamp).LocalDateTime
    Write-Host "✅ timestamp: $($response.timestamp) ($date)" -ForegroundColor Green
} else {
    Write-Host "❌ timestamp ausente" -ForegroundColor Red
}
```

---

### **Comparar Dos Versiones**

```powershell
# Primera ingesta
$body1 = @{ owner = "test"; repo = "sample"; branch = "main"; path = "v1" } | ConvertTo-Json
$v1 = Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" -Method Post -Body $body1 -ContentType "application/json"

Start-Sleep -Seconds 2

# Segunda ingesta
$body2 = @{ owner = "test"; repo = "sample"; branch = "main"; path = "v2" } | ConvertTo-Json
$v2 = Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" -Method Post -Body $body2 -ContentType "application/json"

# Comparar
Write-Host "`nComparacion de Versiones:" -ForegroundColor Cyan
Write-Host "V1 ID: $($v1.versionId)" -ForegroundColor Yellow
Write-Host "V2 ID: $($v2.versionId)" -ForegroundColor Yellow

if ($v1.versionId -ne $v2.versionId) {
    Write-Host "✅ Versiones diferentes" -ForegroundColor Green
} else {
    Write-Host "❌ Versiones iguales (ERROR)" -ForegroundColor Red
}

# Comparar timestamps
$t1 = [DateTimeOffset]::FromUnixTimeSeconds($v1.timestamp).LocalDateTime
$t2 = [DateTimeOffset]::FromUnixTimeSeconds($v2.timestamp).LocalDateTime
$diff = ($t2 - $t1).TotalSeconds

Write-Host "`nDiferencia temporal: $diff segundos" -ForegroundColor Cyan
```

---

## 🔍 Debugging de Logs

### **Filtrar Logs de Mock**

```powershell
# En otra terminal mientras Azure Function corre
# (Requiere tener logs redirigidos a archivo)

# Ver solo logs de Mock Graph
Select-String -Path ".\function-logs.txt" -Pattern "\[MOCK\] Storing graph"

# Ver solo logs de Mock Vector
Select-String -Path ".\function-logs.txt" -Pattern "\[MOCK\] Indexed"

# Ver VersionIds en logs
Select-String -Path ".\function-logs.txt" -Pattern "Version: [a-f0-9-]+"
```

---

### **Capturar Logs en Tiempo Real**

```powershell
# Iniciar Azure Function con logging
func start 2>&1 | Tee-Object -FilePath "function-logs.txt"

# En otra terminal, ver logs en tiempo real
Get-Content "function-logs.txt" -Wait | Select-String -Pattern "\[MOCK\]|Version:"
```

---

## 🎯 Testing de Escenarios Específicos

### **Test: Mismo Repo, Múltiples Commits**

```powershell
$owner = "microsoft"
$repo = "dotnet"
$commits = @("commit1", "commit2", "commit3")
$versions = @()

foreach ($commit in $commits) {
    $body = @{
        owner = $owner
        repo = $repo
        branch = "main"
        path = $commit
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" `
        -Method Post -Body $body -ContentType "application/json"

    $versions += $response.versionId
    Write-Host "Commit: $commit → Version: $($response.versionId)" -ForegroundColor Cyan

    Start-Sleep -Seconds 2
}

# Verificar que todas las versiones son únicas
$uniqueVersions = $versions | Select-Object -Unique
if ($uniqueVersions.Count -eq $versions.Count) {
    Write-Host "`n✅ Todas las versiones son únicas" -ForegroundColor Green
} else {
    Write-Host "`n❌ Hay versiones duplicadas" -ForegroundColor Red
}
```

---

### **Test: Múltiples Repos Simultáneos**

```powershell
$repos = @(
    @{ owner = "org1"; repo = "repo1"; path = "abc" }
    @{ owner = "org2"; repo = "repo2"; path = "def" }
    @{ owner = "org3"; repo = "repo3"; path = "ghi" }
)

$jobs = @()

foreach ($r in $repos) {
    $body = $r | ConvertTo-Json

    # Ejecutar en paralelo (background jobs)
    $job = Start-Job -ScriptBlock {
        param($url, $body)
        Invoke-RestMethod -Uri $url -Method Post -Body $body -ContentType "application/json"
    } -ArgumentList "http://localhost:7071/api/ingest", $body

    $jobs += $job
}

# Esperar a que terminen
$results = $jobs | Wait-Job | Receive-Job

Write-Host "`nResultados de Ingestas Paralelas:" -ForegroundColor Cyan
foreach ($result in $results) {
    Write-Host "  Repo: $($result.repo) → Version: $($result.versionId)" -ForegroundColor Yellow
}

# Verificar que todas las versiones son únicas
$allVersions = $results | ForEach-Object { $_.versionId }
$uniqueVersions = $allVersions | Select-Object -Unique

if ($uniqueVersions.Count -eq $allVersions.Count) {
    Write-Host "`n✅ Todas las versiones son únicas (OK para paralelismo)" -ForegroundColor Green
} else {
    Write-Host "`n❌ Hay versiones duplicadas (ERROR de concurrencia)" -ForegroundColor Red
}

# Limpiar jobs
$jobs | Remove-Job
```

---

### **Test: Verificar Performance**

```powershell
$iterations = 5
$times = @()

Write-Host "Testing Performance ($iterations iteraciones)..." -ForegroundColor Cyan

for ($i = 1; $i -le $iterations; $i++) {
    $body = @{
        owner = "test"
        repo = "perf"
        branch = "main"
        path = "commit-$i"
    } | ConvertTo-Json

    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    $response = Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" `
        -Method Post -Body $body -ContentType "application/json"

    $sw.Stop()
    $times += $sw.ElapsedMilliseconds

    Write-Host "  Iteracion $i : $($sw.ElapsedMilliseconds) ms" -ForegroundColor Gray

    Start-Sleep -Seconds 1
}

$avg = ($times | Measure-Object -Average).Average
$min = ($times | Measure-Object -Minimum).Minimum
$max = ($times | Measure-Object -Maximum).Maximum

Write-Host "`nEstadisticas:" -ForegroundColor Cyan
Write-Host "  Promedio: $([math]::Round($avg, 2)) ms" -ForegroundColor Yellow
Write-Host "  Minimo: $min ms" -ForegroundColor Green
Write-Host "  Maximo: $max ms" -ForegroundColor Red
```

---

## 🗄️ Neo4j Verification

### **Queries Útiles**

```cypher
// Ver todas las versiones
MATCH (v:Version) 
RETURN v.versionId, v.timestamp, v.commitHash, v.isCurrent
ORDER BY v.timestamp DESC

// Ver CodeNodes de una versión específica
MATCH (n:CodeNode)
WHERE n.versionId = "tu-version-id-aqui"
RETURN n.id, n.content, n.validFrom, n.validTo
LIMIT 10

// Contar CodeNodes por versión
MATCH (n:CodeNode)
RETURN n.versionId, count(*) as count
ORDER BY count DESC

// Ver historial de un código específico
MATCH (n:CodeNode)
WHERE n.id CONTAINS "MyClass"
RETURN n.id, n.versionId, n.validFrom, n.validTo
ORDER BY n.validFrom

// Verificar versiones actuales
MATCH (v:Version {isCurrent: true})
RETURN count(*) as currentVersions

// Ver graph + vector consistency
MATCH (v:Version)
OPTIONAL MATCH (n:CodeNode {versionId: v.versionId})
RETURN v.versionId, v.timestamp, count(n) as codeNodes
ORDER BY v.timestamp DESC
```

---

### **PowerShell + Neo4j**

```powershell
# Ejecutar query Cypher desde PowerShell (requiere neo4j driver)
function Invoke-CypherQuery {
    param(
        [string]$Query,
        [string]$Uri = "bolt://localhost:7687",
        [string]$User = "neo4j",
        [string]$Password = "password"
    )

    # Usar neo4j-shell si está instalado
    $cypherShell = "cypher-shell"

    $result = & $cypherShell -a $Uri -u $User -p $Password $Query

    return $result
}

# Ejemplo: Ver versiones
Invoke-CypherQuery -Query "MATCH (v:Version) RETURN v.versionId, v.timestamp LIMIT 5"
```

---

## 📊 Análisis de Response JSON

### **Pretty Print**

```powershell
# Request con formato bonito
$response = Invoke-RestMethod -Uri "http://localhost:7071/api/ingest" `
    -Method Post -Body $body -ContentType "application/json"

# Formato legible
$response | ConvertTo-Json -Depth 10 | Write-Host -ForegroundColor Yellow

# Guardar en archivo
$response | ConvertTo-Json -Depth 10 | Out-File "response.json"

# Ver en VSCode
code response.json
```

---

### **Extraer Metadata**

```powershell
# Crear objeto con solo metadata de versión
$metadata = [PSCustomObject]@{
    VersionId = $response.versionId
    Timestamp = $response.timestamp
    CommitHash = $response.commitHash
    Repo = $response.repo
    Date = [DateTimeOffset]::FromUnixTimeSeconds($response.timestamp).LocalDateTime
}

$metadata | Format-Table -AutoSize
```

---

## 🔄 Switch Mock ↔ Neo4j

### **Script de Toggle**

```powershell
# toggle-provider.ps1
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Mock", "Neo4j")]
    [string]$Provider
)

$settingsFile = ".\AriadnaKnowledgeStore.Functions\local.settings.json"

if (-not (Test-Path $settingsFile)) {
    Write-Host "❌ No se encuentra local.settings.json" -ForegroundColor Red
    exit 1
}

$settings = Get-Content $settingsFile | ConvertFrom-Json

if ($Provider -eq "Mock") {
    $settings.GraphStore.Type = "Mock"
    Write-Host "✅ Cambiado a Mock" -ForegroundColor Green
} else {
    $settings.GraphStore.Type = "Neo4jVersioned"
    Write-Host "✅ Cambiado a Neo4j" -ForegroundColor Green
}

$settings | ConvertTo-Json -Depth 10 | Set-Content $settingsFile

Write-Host "Reinicia Azure Function para aplicar cambios" -ForegroundColor Yellow
```

**Uso:**

```powershell
.\toggle-provider.ps1 -Provider Mock    # Cambiar a Mock
.\toggle-provider.ps1 -Provider Neo4j   # Cambiar a Neo4j
```

---

## 🧹 Cleanup

### **Limpiar Neo4j**

```cypher
// Borrar TODO (¡CUIDADO!)
MATCH (n) DETACH DELETE n

// Borrar solo una versión específica
MATCH (v:Version {versionId: "tu-version-id"})
OPTIONAL MATCH (n:CodeNode {versionId: v.versionId})
DETACH DELETE v, n

// Borrar versiones antiguas (mantener últimas 5)
MATCH (v:Version)
WITH v ORDER BY v.timestamp DESC SKIP 5
OPTIONAL MATCH (n:CodeNode {versionId: v.versionId})
DETACH DELETE v, n
```

---

### **Limpiar Temp Files**

```powershell
# Borrar archivos temporales de descargas
Remove-Item "C:\temp\github-*" -Recurse -Force

# Borrar logs
Remove-Item ".\function-logs.txt" -Force
Remove-Item ".\response.json" -Force
```

---

**Creado:** 2024  
**Propósito:** Comandos útiles para testing y debugging  
**Uso:** Copy-paste en PowerShell según necesites
