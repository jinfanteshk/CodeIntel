# Script para probar ingesta local de JadeiteSocialNetwork
# Requisitos: Azure Functions Core Tools corriendo (func start)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CodeIntel - Test Ingesta Local" -ForegroundColor Cyan
Write-Host "Repositorio: JadeiteSocialNetwork" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuración
$functionUrl = "http://localhost:7071/api/ingest"
$owner = "jesusinfantes-hkteck"
$repo = "JadeiteSocialNetwork"
$branch = "master"  # Cambia si es "main"

Write-Host "📋 Configuración:" -ForegroundColor Yellow
Write-Host "   Owner:  $owner" -ForegroundColor Gray
Write-Host "   Repo:   $repo" -ForegroundColor Gray
Write-Host "   Branch: $branch" -ForegroundColor Gray
Write-Host "   URL:    $functionUrl" -ForegroundColor Gray
Write-Host ""

# Verificar que Azure Functions esté corriendo
Write-Host "🔍 Verificando que Azure Functions esté corriendo..." -ForegroundColor Yellow
try {
    $healthCheck = Invoke-WebRequest -Uri "http://localhost:7071" -Method GET -ErrorAction Stop -TimeoutSec 5
    Write-Host "✅ Azure Functions está corriendo" -ForegroundColor Green
}
catch {
    Write-Host "❌ ERROR: Azure Functions no está corriendo en http://localhost:7071" -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 Ejecuta primero en otra terminal:" -ForegroundColor Yellow
    Write-Host "   cd CodeIntel.Functions" -ForegroundColor Gray
    Write-Host "   func start" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "🚀 Iniciando ingesta..." -ForegroundColor Yellow

# Payload para la función
$payload = @{
    owner = $owner
    repo = $repo
    branch = $branch
    path = ""  # "" para todo el repo
} | ConvertTo-Json

Write-Host "📦 Payload:" -ForegroundColor Gray
Write-Host $payload -ForegroundColor DarkGray
Write-Host ""

# Llamar a la función (esto puede tomar varios minutos)
Write-Host "⏳ Procesando repositorio (esto puede tardar 2-5 minutos)..." -ForegroundColor Yellow
Write-Host "   - Descargando código de GitHub" -ForegroundColor DarkGray
Write-Host "   - Analizando archivos .cs y .aspx" -ForegroundColor DarkGray
Write-Host "   - Creando nodos y relaciones en Neo4j" -ForegroundColor DarkGray
Write-Host "   - Generando embeddings (si está configurado)" -ForegroundColor DarkGray
Write-Host ""

$startTime = Get-Date

try {
    $response = Invoke-RestMethod `
        -Uri $functionUrl `
        -Method POST `
        -Body $payload `
        -ContentType "application/json" `
        -TimeoutSec 600  # 10 minutos timeout

    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds

    Write-Host ""
    Write-Host "✅ ¡Ingesta completada exitosamente!" -ForegroundColor Green
    Write-Host "⏱️  Duración: $([math]::Round($duration, 2)) segundos" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "📊 Resultados:" -ForegroundColor Yellow
    Write-Host ($response | ConvertTo-Json -Depth 10) -ForegroundColor Gray
    Write-Host ""

    # Resumen de nodos creados
    Write-Host "📈 Resumen de nodos creados:" -ForegroundColor Cyan
    Write-Host "   Clases:        $($response.classes)" -ForegroundColor White
    Write-Host "   Métodos:       $($response.methods)" -ForegroundColor White
    Write-Host "   Páginas ASPX:  $($response.aspxPages)" -ForegroundColor White
    Write-Host "   Controles:     $($response.aspxControls)" -ForegroundColor White
    Write-Host "   Eventos ASPX:  $($response.aspxEvents)" -ForegroundColor White
    Write-Host "   Relaciones:    $($response.edges)" -ForegroundColor White
    Write-Host ""

    # Queries para Neo4j
    Write-Host "🔍 Verifica los datos en Neo4j Browser:" -ForegroundColor Yellow
    Write-Host "   URL: https://console.neo4j.io/" -ForegroundColor Gray
    Write-Host ""
    Write-Host "📝 Queries útiles:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "// Ver todas las clases del repositorio" -ForegroundColor DarkGray
    Write-Host "MATCH (c:Class {repoId: `"$owner/$repo@$branch`"})" -ForegroundColor White
    Write-Host "RETURN c.name, c.namespace, c.filePath" -ForegroundColor White
    Write-Host "LIMIT 20" -ForegroundColor White
    Write-Host ""
    Write-Host "// Ver metodos de una clase especifica" -ForegroundColor DarkGray
    Write-Host 'MATCH (c:Class)-[r:BELONGS_TO]-(m:Method)' -ForegroundColor White
    Write-Host "WHERE c.repoId = '$owner/$repo@$branch' AND c.name = 'TU_CLASE'" -ForegroundColor White
    Write-Host "RETURN m.name, m.returnType, m.accessibility" -ForegroundColor White
    Write-Host ""
    Write-Host "// Ver páginas ASPX" -ForegroundColor DarkGray
    Write-Host "MATCH (p:AspxPage {repoId: `"$owner/$repo@$branch`"})" -ForegroundColor White
    Write-Host "RETURN p.name, p.filePath, p.codeBehindClass" -ForegroundColor White
    Write-Host ""
    Write-Host "// Ver el grafo completo" -ForegroundColor DarkGray
    Write-Host "MATCH (n {repoId: `"$owner/$repo@$branch`"})" -ForegroundColor White
    Write-Host "RETURN n LIMIT 100" -ForegroundColor White
    Write-Host ""
    Write-Host "// Ver todas las versiones" -ForegroundColor DarkGray
    Write-Host "MATCH (r:Repository {id: `"$owner/$repo@$branch`"})-[:HAS_VERSION]->(v:Version)" -ForegroundColor White
    Write-Host "RETURN v.id, v.commitHash, datetime({epochSeconds: v.timestamp}) AS time, v.isCurrent" -ForegroundColor White
    Write-Host "ORDER BY v.timestamp DESC" -ForegroundColor White
    Write-Host ""

}
catch {
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds

    Write-Host ""
    Write-Host "❌ ERROR durante la ingesta" -ForegroundColor Red
    Write-Host "⏱️  Duración hasta el error: $([math]::Round($duration, 2)) segundos" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Detalles del error:" -ForegroundColor Yellow
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Posibles causas:" -ForegroundColor Yellow
    Write-Host "   1. Token de GitHub invalido o sin permisos" -ForegroundColor Gray
    Write-Host "   2. Repositorio privado sin acceso" -ForegroundColor Gray
    Write-Host "   3. Neo4j no esta accesible" -ForegroundColor Gray
    Write-Host "   4. Timeout (repositorio muy grande)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Verifica la configuracion en:" -ForegroundColor Yellow
    Write-Host "   CodeIntel.Functions/local.settings.json" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✅ Proceso completado" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
