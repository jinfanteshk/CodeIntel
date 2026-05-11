# test-versioning.ps1
# Script de testing para el sistema de versionado vectorial con Mocks

Write-Host "🧪 Testing Versionado Vectorial con Mocks" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Configuración
$functionUrl = "http://localhost:7071/api/ingest"
$testPassed = 0
$testFailed = 0

function Test-Endpoint {
    param (
        [string]$TestName,
        [hashtable]$Body,
        [scriptblock]$Validator
    )

    Write-Host "`n$TestName..." -ForegroundColor Yellow

    try {
        $bodyJson = $Body | ConvertTo-Json
        $response = Invoke-RestMethod -Uri $functionUrl `
            -Method Post -Body $bodyJson -ContentType "application/json" `
            -ErrorAction Stop

        $result = & $Validator $response

        if ($result) {
            Write-Host "✅ PASADO" -ForegroundColor Green
            $script:testPassed++
            return $response
        } else {
            Write-Host "❌ FALLIDO" -ForegroundColor Red
            $script:testFailed++
            return $null
        }
    } catch {
        Write-Host "❌ ERROR: $_" -ForegroundColor Red
        $script:testFailed++
        return $null
    }
}

# Test 1: Primera Ingesta - Verificar VersionContext
Write-Host "`n═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Test 1: Primera Ingesta" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan

$response1 = Test-Endpoint `
    -TestName "Verificando primera ingesta y VersionContext" `
    -Body @{
        owner = "test"
        repo = "sample"
        branch = "main"
        path = "commit-v1"
    } `
    -Validator {
        param($r)
        if ($r.versionId -and $r.timestamp -and $r.commitHash) {
            Write-Host "  versionId: $($r.versionId)" -ForegroundColor Gray
            Write-Host "  timestamp: $($r.timestamp)" -ForegroundColor Gray
            Write-Host "  commitHash: $($r.commitHash)" -ForegroundColor Gray
            Write-Host "  repo: $($r.repo)" -ForegroundColor Gray
            return $true
        }
        return $false
    }

if (-not $response1) {
    Write-Host "`n❌ Test 1 falló. Abortando tests." -ForegroundColor Red
    exit 1
}

Start-Sleep -Seconds 2

# Test 2: Segunda Ingesta - Verificar Versiones Únicas
Write-Host "`n═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Test 2: Segunda Ingesta (Versión Única)" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan

$response2 = Test-Endpoint `
    -TestName "Verificando segunda ingesta con versión diferente" `
    -Body @{
        owner = "test"
        repo = "sample"
        branch = "main"
        path = "commit-v2"
    } `
    -Validator {
        param($r)
        if ($r.versionId -and $r.versionId -ne $response1.versionId) {
            Write-Host "  Version 1: $($response1.versionId)" -ForegroundColor Gray
            Write-Host "  Version 2: $($r.versionId)" -ForegroundColor Gray
            Write-Host "  ✅ Versiones diferentes" -ForegroundColor Green
            return $true
        } else {
            Write-Host "  ❌ Versiones iguales o falta versionId" -ForegroundColor Red
            return $false
        }
    }

Start-Sleep -Seconds 2

# Test 3: Verificar Todos los Campos Requeridos
Write-Host "`n═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Test 3: Verificación de Campos" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan

$response3 = Test-Endpoint `
    -TestName "Verificando campos requeridos en response" `
    -Body @{
        owner = "test"
        repo = "sample"
        branch = "main"
        path = "commit-v3"
    } `
    -Validator {
        param($r)
        $requiredFields = @(
            "versionId", "timestamp", "commitHash", "repo", 
            "classes", "methods", "indexed", "chunksGenerated"
        )

        $allPresent = $true
        foreach ($field in $requiredFields) {
            if ($r.PSObject.Properties[$field]) {
                Write-Host "  ✅ $field : $($r.$field)" -ForegroundColor Green
            } else {
                Write-Host "  ❌ Falta: $field" -ForegroundColor Red
                $allPresent = $false
            }
        }
        return $allPresent
    }

Start-Sleep -Seconds 2

# Test 4: Ingesta Múltiple de Diferentes Repos
Write-Host "`n═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Test 4: Múltiples Repos" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan

$responseA = Test-Endpoint `
    -TestName "Repo A" `
    -Body @{
        owner = "org1"
        repo = "repoA"
        branch = "main"
        path = "commit-a1"
    } `
    -Validator {
        param($r)
        return ($r.versionId -ne $null)
    }

Start-Sleep -Seconds 1

$responseB = Test-Endpoint `
    -TestName "Repo B" `
    -Body @{
        owner = "org2"
        repo = "repoB"
        branch = "main"
        path = "commit-b1"
    } `
    -Validator {
        param($r)
        if ($r.versionId -and $r.versionId -ne $responseA.versionId -and $r.repo -ne $responseA.repo) {
            Write-Host "  Repo A: $($responseA.repo) → Version: $($responseA.versionId)" -ForegroundColor Gray
            Write-Host "  Repo B: $($r.repo) → Version: $($r.versionId)" -ForegroundColor Gray
            return $true
        }
        return $false
    }

# Test 5: Verificar Consistency en Logs
Write-Host "`n═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Test 5: Manual - Verificación de Logs" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan

Write-Host @"

Por favor verifica MANUALMENTE en la consola de Azure Functions:

1. ✅ Busca: [MOCK] Storing graph: ... Version: <guid>
2. ✅ Busca: [MOCK] Indexed ... documents for version <guid>
3. ✅ Verifica que el <guid> sea EL MISMO en ambos logs
4. ✅ Verifica que aparece: "✅ Stored graph with version: <guid>"
5. ✅ Verifica que aparece: "✅ Indexed ... documents with version: <guid>"

"@ -ForegroundColor Yellow

Read-Host "Presiona Enter cuando hayas verificado los logs"

# Resumen Final
Write-Host "`n════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "RESULTADO FINAL" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan

Write-Host "`nTests Automáticos:" -ForegroundColor White
Write-Host "  ✅ Pasados: $testPassed" -ForegroundColor Green
Write-Host "  ❌ Fallidos: $testFailed" -ForegroundColor Red

if ($testFailed -eq 0) {
    Write-Host "`n✅✅✅ TODOS LOS TESTS PASARON ✅✅✅" -ForegroundColor Green
    Write-Host "`nSistema validado con Mocks." -ForegroundColor Green
    Write-Host "Listo para probar con Neo4j:" -ForegroundColor Green
    Write-Host "  1. Cambia GraphStore:Type a 'Neo4jVersioned' en appsettings.json" -ForegroundColor Yellow
    Write-Host "  2. Configura Neo4j:Password" -ForegroundColor Yellow
    Write-Host "  3. Reinicia Azure Function" -ForegroundColor Yellow
    Write-Host "  4. Re-ejecuta este script" -ForegroundColor Yellow
} else {
    Write-Host "`n❌ ALGUNOS TESTS FALLARON" -ForegroundColor Red
    Write-Host "Revisa la implementación antes de continuar." -ForegroundColor Red
    exit 1
}

Write-Host "`n════════════════════════════════════════`n" -ForegroundColor Cyan
