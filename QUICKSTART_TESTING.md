# 🚀 INICIO RÁPIDO - Testing con Mocks

## ⚡ 3 Pasos para Empezar

### **Paso 1: Configurar Mocks** (2 minutos)

```powershell
# Navegar al directorio de Functions
cd C:\proyectos\gh-ariadna-knowledgestore-mvp\src\AriadnaKnowledgeStore.Functions

# Copiar ejemplo de configuración
Copy-Item local.settings.json.example local.settings.json

# Editar local.settings.json y agregar tu GitHub token:
notepad local.settings.json
```

**Configuración mínima en `local.settings.json`:**

```json
{
  "GitHub": {
    "Token": "ghp_TU_TOKEN_AQUI"
  },
  "GraphStore": {
    "Type": "Mock"
  }
}
```

---

### **Paso 2: Iniciar Azure Function** (1 minuto)

```powershell
# En la misma terminal
func start
```

**Espera ver:**
```
Azure Functions Core Tools
...
Functions:
  ingest: [POST] http://localhost:7071/api/ingest
```

---

### **Paso 3: Ejecutar Tests** (1 minuto)

```powershell
# Abre OTRA terminal PowerShell
cd C:\proyectos\gh-ariadna-knowledgestore-mvp\src

# Ejecutar script de testing
.\test-versioning.ps1
```

---

## 📊 Resultado Esperado

```
🧪 Testing Versionado Vectorial con Mocks
========================================

Test 1: Primera Ingesta
✅ PASADO
  versionId: 12345-abc...
  timestamp: 1704067200
  commitHash: commit-v1

Test 2: Segunda Ingesta (Versión Única)
✅ PASADO
  Version 1: 12345-abc...
  Version 2: 67890-def...
  ✅ Versiones diferentes

Test 3: Verificación de Campos
✅ PASADO
  ✅ versionId : 11111-ghi...
  ✅ timestamp : 1704067250
  ✅ commitHash : commit-v3
  ✅ repo : test/sample@main
  ✅ classes : 5
  ✅ methods : 20
  ✅ indexed : 25
  ✅ chunksGenerated : 25

✅✅✅ TODOS LOS TESTS PASARON ✅✅✅
```

---

## ✅ Verificación Manual (Logs)

En la terminal donde corre Azure Function, busca:

```
[MOCK] Storing graph: 5 classes, 20 methods, Version: 12345-abc...
✅ Stored graph with version: 12345-abc...
[MOCK] Indexed 25 documents for version 12345-abc...
✅ Indexed 25 documents with version: 12345-abc...
```

**Importante:** El `versionId` debe ser **EL MISMO** entre graph y vector.

---

## 🔄 Cambiar a Neo4j

Una vez que los tests con Mock pasen:

1. **Editar `local.settings.json`:**
```json
{
  "GraphStore": {
    "Type": "Neo4jVersioned"
  },
  "Neo4j": {
    "Uri": "bolt://localhost:7687",
    "User": "neo4j",
    "Password": "tu-password-aqui"
  }
}
```

2. **Reiniciar Azure Function:**
```powershell
# Ctrl+C para detener
func start  # Reiniciar
```

3. **Re-ejecutar tests:**
```powershell
.\test-versioning.ps1
```

4. **Verificar en Neo4j Browser:**
```cypher
// Ver versiones
MATCH (v:Version) RETURN v ORDER BY v.timestamp DESC LIMIT 5

// Ver CodeNodes versionados
MATCH (n:CodeNode) 
RETURN n.id, n.versionId, n.validFrom, n.validTo 
LIMIT 10
```

---

## 🐛 Troubleshooting Rápido

| Error | Solución |
|-------|----------|
| "GitHub:Token missing" | Agregar token en `local.settings.json` |
| "Neo4j:Password missing" | Cambiar `GraphStore:Type` a `"Mock"` |
| Puerto 7071 ocupado | `func start --port 7072` |
| No se ven logs | Verificar `LogLevel: Information` |
| Tests fallan | Verificar que Azure Function esté corriendo |

---

## 📚 Documentación Completa

Para más detalles, ver:
- `docs\TESTING_WITH_MOCKS.md` - Guía completa de testing
- `docs\VECTOR_VERSIONING_IMPLEMENTATION_COMPLETE.md` - Detalles de implementación
- `docs\VERSIONING_SYSTEM_EXPLAINED.md` - Explicación del sistema de versionado

---

**¡Listo!** En menos de 5 minutos deberías estar ejecutando tests con Mocks.
