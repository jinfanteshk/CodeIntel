# Script para verificar los nodos creados en Neo4j después de la ingesta

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CodeIntel - Verificación Neo4j" -ForegroundColor Cyan
Write-Host "Repositorio: JadeiteSocialNetwork" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuración (debe coincidir con local.settings.json)
$neo4jUri = "neo4j+s://503c34a2.databases.neo4j.io"
$neo4jUser = "neo4j"
$neo4jPassword = "Es5RDiQ-5yfsz76r70iyNTqvIHKj_ueyKbGqZNSm9NU"

$repoId = "jesusinfantes-hkteck/JadeiteSocialNetwork@main"  # Rama principal

Write-Host "🔍 Verificando conexión a Neo4j..." -ForegroundColor Yellow
Write-Host "   URI: $neo4jUri" -ForegroundColor Gray
Write-Host ""

# Nota: Este script muestra las queries que debes ejecutar manualmente
# PowerShell no tiene un driver Neo4j nativo simple

Write-Host "📝 Queries de verificación para ejecutar en Neo4j Browser:" -ForegroundColor Cyan
Write-Host "   URL: https://console.neo4j.io/projects/2a4895fc-e83c-4a1e-987f-f918237f8667/tools/explore" -ForegroundColor Gray
Write-Host ""

Write-Host "==========================================" -ForegroundColor Yellow
Write-Host "1️⃣  VERIFICAR REPOSITORIO Y VERSIONES" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "// Ver si el repositorio fue creado" -ForegroundColor DarkGray
Write-Host @"
MATCH (r:Repository {id: "$repoId"})
RETURN r
"@ -ForegroundColor White
Write-Host ""

Write-Host "// Ver versiones del repositorio" -ForegroundColor DarkGray
Write-Host @"
MATCH (r:Repository {id: "$repoId"})-[:HAS_VERSION]->(v:Version)
RETURN v.id AS versionId, 
       v.commitHash AS commit,
       datetime({epochSeconds: v.timestamp}) AS timestamp,
       v.isCurrent AS current
ORDER BY v.timestamp DESC
"@ -ForegroundColor White
Write-Host ""

Write-Host "==========================================" -ForegroundColor Yellow
Write-Host "2️⃣  CONTAR NODOS CREADOS" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "// Resumen de todo lo creado" -ForegroundColor DarkGray
Write-Host @"
MATCH (c:Class {repoId: "$repoId"})
WITH count(c) AS totalClasses
MATCH (m:Method {repoId: "$repoId"})
WITH totalClasses, count(m) AS totalMethods
MATCH (p:AspxPage {repoId: "$repoId"})
WITH totalClasses, totalMethods, count(p) AS totalPages
MATCH (ctrl:AspxControl {repoId: "$repoId"})
WITH totalClasses, totalMethods, totalPages, count(ctrl) AS totalControls
MATCH (e:AspxEvent {repoId: "$repoId"})
RETURN 
  totalClasses AS `Clases`,
  totalMethods AS `Métodos`,
  totalPages AS `Páginas ASPX`,
  totalControls AS `Controles`,
  count(e) AS `Eventos ASPX`
"@ -ForegroundColor White
Write-Host ""

Write-Host "==========================================" -ForegroundColor Yellow
Write-Host "3️⃣  EXPLORAR CLASES" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "// Top 20 clases del repositorio" -ForegroundColor DarkGray
Write-Host @"
MATCH (c:Class {repoId: "$repoId"})
RETURN c.name AS Clase,
       c.namespace AS Namespace,
       c.filePath AS Archivo,
       c.accessibility AS Acceso
ORDER BY c.name
LIMIT 20
"@ -ForegroundColor White
Write-Host ""

Write-Host "// Clases con más métodos" -ForegroundColor DarkGray
Write-Host @"
MATCH (c:Class {repoId: "$repoId"})<-[:BELONGS_TO]-(m:Method)
WITH c, count(m) AS numMethods
RETURN c.name AS Clase,
       c.namespace AS Namespace,
       numMethods AS `Número de Métodos`
ORDER BY numMethods DESC
LIMIT 10
"@ -ForegroundColor White
Write-Host ""

Write-Host "==========================================" -ForegroundColor Yellow
Write-Host "4️⃣  EXPLORAR PÁGINAS ASPX (si existen)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "// Ver todas las páginas ASPX" -ForegroundColor DarkGray
Write-Host @"
MATCH (p:AspxPage {repoId: "$repoId"})
RETURN p.name AS Página,
       p.filePath AS Archivo,
       p.codeBehindClass AS CodeBehind
LIMIT 20
"@ -ForegroundColor White
Write-Host ""

Write-Host "// Páginas ASPX con sus controles" -ForegroundColor DarkGray
Write-Host @"
MATCH (p:AspxPage {repoId: "$repoId"})-[:HAS_CONTROL]->(ctrl:AspxControl)
RETURN p.name AS Página,
       collect(ctrl.name) AS Controles
LIMIT 10
"@ -ForegroundColor White
Write-Host ""

Write-Host "==========================================" -ForegroundColor Yellow
Write-Host "5️⃣  EXPLORAR DEPENDENCIAS" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "// Ver qué métodos llaman a otros métodos" -ForegroundColor DarkGray
Write-Host @"
MATCH (m1:Method {repoId: "$repoId"})-[:CALLS]->(m2:Method)
RETURN m1.name AS Llamador,
       m2.name AS Llamado,
       m1.className AS ClaseOrigen
LIMIT 20
"@ -ForegroundColor White
Write-Host ""

Write-Host "// Ver qué clases dependen de otras clases" -ForegroundColor DarkGray
Write-Host @"
MATCH (c1:Class {repoId: "$repoId"})-[:DEPENDS_ON]->(c2:Class)
RETURN c1.name AS ClaseOrigen,
       c2.name AS ClaseDestino
LIMIT 20
"@ -ForegroundColor White
Write-Host ""

Write-Host "==========================================" -ForegroundColor Yellow
Write-Host "6️⃣  VISUALIZAR GRAFO" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "// Visualizar primeros 50 nodos del repo" -ForegroundColor DarkGray
Write-Host @"
MATCH (n {repoId: "$repoId"})
RETURN n
LIMIT 50
"@ -ForegroundColor White
Write-Host ""

Write-Host "// Visualizar una clase específica con sus métodos" -ForegroundColor DarkGray
Write-Host @"
MATCH path = (c:Class {repoId: "$repoId", name: "TU_CLASE_AQUI"})<-[:BELONGS_TO]-(m:Method)
RETURN path
LIMIT 20
"@ -ForegroundColor White
Write-Host ""

Write-Host "// Ver grafo de dependencias de una clase" -ForegroundColor DarkGray
Write-Host @"
MATCH path = (c:Class {repoId: "$repoId", name: "TU_CLASE_AQUI"})-[:DEPENDS_ON*1..2]-(other)
RETURN path
LIMIT 30
"@ -ForegroundColor White
Write-Host ""

Write-Host "==========================================" -ForegroundColor Yellow
Write-Host "7️⃣  QUERIES TEMPORALES (VERSIONADO)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "// Ver clases válidas AHORA (versión actual)" -ForegroundColor DarkGray
Write-Host @"
MATCH (c:Class {repoId: "$repoId"})
WHERE c.validTo IS NULL OR c.validTo > timestamp()
RETURN c.name, c.namespace, c.versionId
LIMIT 20
"@ -ForegroundColor White
Write-Host ""

Write-Host "// Ver todas las versiones de una clase específica" -ForegroundColor DarkGray
Write-Host @"
MATCH (c:Class {repoId: "$repoId", name: "TU_CLASE_AQUI"})
RETURN c.name,
       c.versionId,
       datetime({epochSeconds: c.validFrom}) AS desde,
       CASE 
         WHEN c.validTo IS NULL THEN "Actual"
         ELSE toString(datetime({epochSeconds: c.validTo}))
       END AS hasta
ORDER BY c.validFrom DESC
"@ -ForegroundColor White
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "💡 TIPS:" -ForegroundColor Yellow
Write-Host "   1. Ejecuta estas queries en Neo4j Browser para explorar los datos" -ForegroundColor Gray
Write-Host "   2. Cambia 'TU_CLASE_AQUI' por el nombre real de una clase" -ForegroundColor Gray
Write-Host "   3. Ajusta los LIMIT según necesites más o menos resultados" -ForegroundColor Gray
Write-Host "   4. Usa el modo de visualización (graph view) para ver las relaciones" -ForegroundColor Gray
Write-Host ""
Write-Host "🌐 Neo4j Console:" -ForegroundColor Cyan
Write-Host "   https://console.neo4j.io/projects/2a4895fc-e83c-4a1e-987f-f918237f8667/tools/explore" -ForegroundColor White
Write-Host ""
