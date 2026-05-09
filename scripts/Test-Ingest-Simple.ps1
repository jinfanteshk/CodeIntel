# Script para probar ingesta local de JadeiteSocialNetwork
# Requisitos: Azure Functions Core Tools corriendo (func start)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CodeIntel - Test Ingesta Local" -ForegroundColor Cyan
Write-Host "Repositorio: JadeiteSocialNetwork" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuracion
$functionUrl = "http://localhost:7071/api/ingest"
$owner = "jesusinfantes-hkteck"
$repo = "JadeiteSocialNetwork"
$branch = "main"  # Rama principal del repositorio

Write-Host "Configuracion:" -ForegroundColor Yellow
Write-Host "   Owner:  $owner" -ForegroundColor Gray
Write-Host "   Repo:   $repo" -ForegroundColor Gray
Write-Host "   Branch: $branch" -ForegroundColor Gray
Write-Host "   URL:    $functionUrl" -ForegroundColor Gray
Write-Host ""

# Verificar que Azure Functions este corriendo
Write-Host "Verificando que Azure Functions este corriendo..." -ForegroundColor Yellow
try {
    $healthCheck = Invoke-WebRequest -Uri "http://localhost:7071" -Method GET -ErrorAction Stop -TimeoutSec 5
    Write-Host "OK - Azure Functions esta corriendo" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: Azure Functions no esta corriendo en http://localhost:7071" -ForegroundColor Red
    Write-Host ""
    Write-Host "Ejecuta primero en otra terminal:" -ForegroundColor Yellow
    Write-Host "   cd CodeIntel.Functions" -ForegroundColor Gray
    Write-Host "   func start" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "Iniciando ingesta..." -ForegroundColor Yellow

# Payload para la funcion
$payload = @{
    owner = $owner
    repo = $repo
    branch = $branch
    path = ""
} | ConvertTo-Json

Write-Host "Payload:" -ForegroundColor Gray
Write-Host $payload -ForegroundColor DarkGray
Write-Host ""

# Llamar a la funcion (esto puede tomar varios minutos)
Write-Host "Procesando repositorio (esto puede tardar 2-5 minutos)..." -ForegroundColor Yellow
Write-Host "   - Descargando codigo de GitHub" -ForegroundColor DarkGray
Write-Host "   - Analizando archivos .cs y .aspx" -ForegroundColor DarkGray
Write-Host "   - Creando nodos y relaciones en Neo4j" -ForegroundColor DarkGray
Write-Host ""

$startTime = Get-Date

try {
    $response = Invoke-RestMethod `
        -Uri $functionUrl `
        -Method POST `
        -Body $payload `
        -ContentType "application/json" `
        -TimeoutSec 600

    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds

    Write-Host ""
    Write-Host "Ingesta completada exitosamente!" -ForegroundColor Green
    Write-Host "Duracion: $([math]::Round($duration, 2)) segundos" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Resultados:" -ForegroundColor Yellow
    Write-Host ($response | ConvertTo-Json -Depth 10) -ForegroundColor Gray
    Write-Host ""

    # Resumen de nodos creados
    Write-Host "Resumen de nodos creados:" -ForegroundColor Cyan
    Write-Host "   Clases:        $($response.classes)" -ForegroundColor White
    Write-Host "   Metodos:       $($response.methods)" -ForegroundColor White
    Write-Host "   Paginas ASPX:  $($response.aspxPages)" -ForegroundColor White
    Write-Host "   Controles:     $($response.aspxControls)" -ForegroundColor White
    Write-Host "   Eventos ASPX:  $($response.aspxEvents)" -ForegroundColor White
    Write-Host "   Relaciones:    $($response.edges)" -ForegroundColor White
    Write-Host ""

    # Queries para Neo4j
    Write-Host "Verifica los datos en Neo4j Browser:" -ForegroundColor Yellow
    Write-Host "   URL: https://console.neo4j.io/" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Queries utiles (copia/pega en Neo4j Browser):" -ForegroundColor Cyan
    Write-Host ""

    $repoId = "$owner/$repo@$branch"

    Write-Host "// Ver todas las clases del repositorio" -ForegroundColor DarkGray
    Write-Host "MATCH (c:Class {repoId: '$repoId'})" -ForegroundColor White
    Write-Host "RETURN c.name, c.namespace, c.filePath" -ForegroundColor White
    Write-Host "LIMIT 20" -ForegroundColor White
    Write-Host ""

    Write-Host "// Ver metodos de una clase" -ForegroundColor DarkGray
    Write-Host "MATCH (c:Class {repoId: '$repoId'})" -ForegroundColor White
    Write-Host "MATCH (m:Method {repoId: '$repoId'})-[:BELONGS_TO]->(c)" -ForegroundColor White
    Write-Host "WHERE c.name = 'Program'" -ForegroundColor White
    Write-Host "RETURN m.name, m.returnType" -ForegroundColor White
    Write-Host ""

    Write-Host "// Ver el grafo completo (cambiar a vista Graph)" -ForegroundColor DarkGray
    Write-Host "MATCH (n {repoId: '$repoId'})" -ForegroundColor White
    Write-Host "RETURN n LIMIT 50" -ForegroundColor White
    Write-Host ""

}
catch {
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds

    Write-Host ""
    Write-Host "ERROR durante la ingesta" -ForegroundColor Red
    Write-Host "Duracion hasta el error: $([math]::Round($duration, 2)) segundos" -ForegroundColor Cyan
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
Write-Host "Proceso completado" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
