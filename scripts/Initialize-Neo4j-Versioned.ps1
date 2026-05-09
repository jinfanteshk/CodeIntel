# Script para inicializar Neo4j con los índices necesarios para CodeIntel
# con soporte de versionado temporal
#
# Uso:
#   Para Neo4j AuraDB Cloud:
#     .\Initialize-Neo4j-Versioned.ps1 -Uri "neo4j+s://abc12345.databases.neo4j.io" -User "neo4j" -Password "tu-password"
#
#   Para Neo4j Local:
#     .\Initialize-Neo4j-Versioned.ps1 -Password "tu-password"

param(
    [string]$Uri = "bolt://localhost:7687",
    [string]$User = "neo4j",
    [string]$Password = "password"
)

Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "CodeIntel - Neo4j Initialization" -ForegroundColor Cyan
Write-Host "Versionado Temporal Neo4j" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

# Verificar que el módulo Neo4j esté instalado
$neo4jModule = Get-Module -ListAvailable -Name Neo4j.Driver.Simple

if (-not $neo4jModule) {
    Write-Host "⚠️  El módulo Neo4j.Driver.Simple no está instalado." -ForegroundColor Yellow
    Write-Host "   Instalando via NuGet Package..." -ForegroundColor Yellow

    # Alternativa: usar Cypher-Shell directamente
    Write-Host ""
    Write-Host "💡 ALTERNATIVA: Usar Cypher-Shell" -ForegroundColor Green
    Write-Host "   Ejecuta estos comandos manualmente en Cypher-Shell o Neo4j Browser:" -ForegroundColor Gray
    Write-Host ""
}

# Queries Cypher para inicialización
$cypherQueries = @"
// ===================================================
// CodeIntel - Índices y Constraints para Neo4j
// Versionado Temporal Neo4j
// ===================================================

// 1. CONSTRAINTS - Unicidad
// --------------------------
CREATE CONSTRAINT repo_id IF NOT EXISTS
FOR (r:Repository) REQUIRE r.id IS UNIQUE;

CREATE CONSTRAINT version_id IF NOT EXISTS
FOR (v:Version) REQUIRE v.id IS UNIQUE;

// Nota: NO creamos constraint único en Class.id ni Method.id
// porque pueden existir múltiples versiones del mismo elemento


// 2. INDICES - Performance
// --------------------------

// Índices para búsquedas por repoId
CREATE INDEX class_repo_id IF NOT EXISTS
FOR (c:Class) ON (c.repoId);

CREATE INDEX method_repo_id IF NOT EXISTS
FOR (m:Method) ON (m.repoId);

// Índices temporales (CRÍTICOS para versionado temporal)
CREATE INDEX class_temporal IF NOT EXISTS
FOR (c:Class) ON (c.validFrom, c.validTo);

CREATE INDEX method_temporal IF NOT EXISTS
FOR (m:Method) ON (m.validFrom, m.validTo);

// Índices para búsquedas por versionId
CREATE INDEX class_version_id IF NOT EXISTS
FOR (c:Class) ON (c.versionId);

CREATE INDEX method_version_id IF NOT EXISTS
FOR (m:Method) ON (m.versionId);

// Índices para búsquedas combinadas (optimización)
CREATE INDEX class_repo_temporal IF NOT EXISTS
FOR (c:Class) ON (c.repoId, c.validFrom, c.validTo);

CREATE INDEX method_repo_temporal IF NOT EXISTS
FOR (m:Method) ON (m.repoId, m.validFrom, m.validTo);

// Índice para Version.isCurrent
CREATE INDEX version_current IF NOT EXISTS
FOR (v:Version) ON (v.isCurrent);

// Índice para Version.timestamp
CREATE INDEX version_timestamp IF NOT EXISTS
FOR (v:Version) ON (v.timestamp);


// 3. VERIFICACIÓN
// ---------------
SHOW CONSTRAINTS;
SHOW INDEXES;


// 4. ESTADÍSTICAS
// ---------------
// Después de cargar datos, actualizar estadísticas para mejor rendimiento:
// CALL db.stats.retrieve('GRAPH COUNTS');
"@

Write-Host "📋 Queries Cypher para ejecutar:" -ForegroundColor Cyan
Write-Host $cypherQueries -ForegroundColor Gray
Write-Host ""

# Guardar queries en archivo
$outputFile = "neo4j-init-versioned.cypher"
$cypherQueries | Out-File -FilePath $outputFile -Encoding UTF8

Write-Host "✅ Queries guardadas en: $outputFile" -ForegroundColor Green
Write-Host ""
Write-Host "📖 INSTRUCCIONES:" -ForegroundColor Yellow
Write-Host "   1. Abre Neo4j Browser: http://localhost:7474" -ForegroundColor White
Write-Host "   2. Conéctate con tus credenciales" -ForegroundColor White
Write-Host "   3. Copia y pega el contenido de '$outputFile'" -ForegroundColor White
Write-Host "   4. Ejecuta las queries" -ForegroundColor White
Write-Host ""
Write-Host "   O usa Cypher-Shell:" -ForegroundColor White
Write-Host "   cypher-shell -u $User -p $Password -f $outputFile" -ForegroundColor Cyan
Write-Host ""

# Intentar conectar y ejecutar si es posible
try {
    Write-Host "🔄 Intentando ejecutar automáticamente..." -ForegroundColor Yellow

    # Verificar si cypher-shell está disponible
    $cypherShellPath = Get-Command cypher-shell -ErrorAction SilentlyContinue

    if ($cypherShellPath) {
        Write-Host "✅ Cypher-Shell encontrado: $($cypherShellPath.Path)" -ForegroundColor Green

        $env:NEO4J_USERNAME = $User
        $env:NEO4J_PASSWORD = $Password

        & cypher-shell -a $Uri -f $outputFile

        Write-Host ""
        Write-Host "✅ Inicialización completada!" -ForegroundColor Green
    }
    else {
        Write-Host "⚠️  Cypher-Shell no encontrado en PATH" -ForegroundColor Yellow
        Write-Host "   Ejecuta manualmente las queries en Neo4j Browser" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "❌ Error al ejecutar: $_" -ForegroundColor Red
    Write-Host "   Ejecuta manualmente las queries en Neo4j Browser" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "Siguiente paso:" -ForegroundColor Cyan
Write-Host "  dotnet run --project CodeIntel.Functions" -ForegroundColor White
Write-Host "=======================================" -ForegroundColor Cyan
