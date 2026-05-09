# Guía de Ejecución Manual - Ingesta JadeiteSocialNetwork
# Si no puedes ejecutar scripts PowerShell, copia/pega estos comandos uno por uno

# ==============================================================================
# PASO 1: VERIFICAR QUE AZURE FUNCTIONS ESTÉ CORRIENDO
# ==============================================================================

# En OTRA terminal debes tener corriendo:
# cd C:\proyectos\gh-code-intel-mvp\src\CodeIntel.Functions
# func start

# Verifica que esté corriendo:
Invoke-WebRequest -Uri "http://localhost:7071" -Method GET -TimeoutSec 5

# Si no está corriendo, verás un error. En ese caso, abre otra terminal y ejecuta "func start"

# ==============================================================================
# PASO 2: PREPARAR PAYLOAD
# ==============================================================================

$payload = @{
    owner = "jesusinfantes-hkteck"
    repo = "JadeiteSocialNetwork"
    branch = "master"  # Cambia a "main" si es necesario
    path = ""
} | ConvertTo-Json

Write-Host "Payload preparado:" -ForegroundColor Green
Write-Host $payload

# ==============================================================================
# PASO 3: LLAMAR A LA FUNCIÓN DE INGESTA
# ==============================================================================

Write-Host ""
Write-Host "Iniciando ingesta (esto puede tardar 2-5 minutos)..." -ForegroundColor Yellow
Write-Host ""

$startTime = Get-Date

$response = Invoke-RestMethod `
    -Uri "http://localhost:7071/api/ingest" `
    -Method POST `
    -Body $payload `
    -ContentType "application/json" `
    -TimeoutSec 600

$endTime = Get-Date
$duration = ($endTime - $startTime).TotalSeconds

# ==============================================================================
# PASO 4: MOSTRAR RESULTADOS
# ==============================================================================

Write-Host ""
Write-Host "✅ Ingesta completada!" -ForegroundColor Green
Write-Host "⏱️  Duración: $([math]::Round($duration, 2)) segundos" -ForegroundColor Cyan
Write-Host ""
Write-Host "📊 Resultados:" -ForegroundColor Yellow
Write-Host ($response | ConvertTo-Json -Depth 10)
Write-Host ""
Write-Host "📈 Resumen:" -ForegroundColor Cyan
Write-Host "   Clases:        $($response.classes)" -ForegroundColor White
Write-Host "   Métodos:       $($response.methods)" -ForegroundColor White
Write-Host "   Páginas ASPX:  $($response.aspxPages)" -ForegroundColor White
Write-Host "   Controles:     $($response.aspxControls)" -ForegroundColor White
Write-Host "   Eventos ASPX:  $($response.aspxEvents)" -ForegroundColor White
Write-Host "   Relaciones:    $($response.edges)" -ForegroundColor White
Write-Host ""

# ==============================================================================
# PASO 5: QUERIES PARA NEO4J
# ==============================================================================

Write-Host "🔍 Verifica los datos en Neo4j Browser:" -ForegroundColor Yellow
Write-Host "   URL: https://console.neo4j.io/projects/2a4895fc-e83c-4a1e-987f-f918237f8667/tools/explore" -ForegroundColor Gray
Write-Host ""
Write-Host "📝 Queries útiles (copia/pega en Neo4j Browser):" -ForegroundColor Cyan
Write-Host ""

$repoId = "jesusinfantes-hkteck/JadeiteSocialNetwork@master"

Write-Host "// 1. Ver repositorio creado" -ForegroundColor DarkGray
Write-Host "MATCH (r:Repository {id: `"$repoId`"})" -ForegroundColor White
Write-Host "RETURN r" -ForegroundColor White
Write-Host ""

Write-Host "// 2. Contar nodos" -ForegroundColor DarkGray
Write-Host @"
MATCH (c:Class {repoId: "$repoId"})
WITH count(c) AS classes
MATCH (m:Method {repoId: "$repoId"})
RETURN classes, count(m) AS methods
"@ -ForegroundColor White
Write-Host ""

Write-Host "// 3. Ver clases" -ForegroundColor DarkGray
Write-Host @"
MATCH (c:Class {repoId: "$repoId"})
RETURN c.name, c.namespace, c.filePath
LIMIT 20
"@ -ForegroundColor White
Write-Host ""

Write-Host "// 4. Visualizar grafo (cambiar a vista Graph)" -ForegroundColor DarkGray
Write-Host @"
MATCH (n {repoId: "$repoId"})
RETURN n
LIMIT 50
"@ -ForegroundColor White
Write-Host ""
