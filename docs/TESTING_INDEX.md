# 📚 ÍNDICE - Documentación de Testing

## 🎯 ¿Por Dónde Empiezo?

### **Si quieres empezar YA (< 5 minutos):**
👉 **[QUICKSTART_TESTING.md](../QUICKSTART_TESTING.md)**

### **Si quieres entender el plan completo:**
👉 **[docs/TESTING_SUMMARY.md](TESTING_SUMMARY.md)**

---

## 📖 Guías por Propósito

### 🚀 **Inicio Rápido**
- **[QUICKSTART_TESTING.md](../QUICKSTART_TESTING.md)**
  - ⚡ 3 pasos para empezar
  - ⏱️ Menos de 5 minutos
  - 🎯 Configuración mínima

### 📚 **Guía Completa de Testing**
- **[TESTING_WITH_MOCKS.md](TESTING_WITH_MOCKS.md)**
  - 📋 Plan de testing detallado
  - 🧪 Tests paso a paso
  - ✅ Checklist de validación
  - 🐛 Troubleshooting completo
  - 📝 Script automatizado explicado

### 📊 **Resumen Ejecutivo**
- **[TESTING_SUMMARY.md](TESTING_SUMMARY.md)**
  - ✅ ¿Qué modificaciones se necesitan? (Respuesta: NINGUNA)
  - 🔍 Cómo funciona el sistema
  - 📊 Estado de componentes
  - 🎬 Pasos para empezar

### 🔧 **Comandos y Utilidades**
- **[TESTING_COMMANDS.md](TESTING_COMMANDS.md)**
  - 📦 Comandos PowerShell útiles
  - 🔍 Scripts de debugging
  - 🧪 Tests individuales
  - 📊 Análisis de responses
  - 🔄 Toggle Mock ↔ Neo4j

### 📊 **Diagramas y Visualización**
- **[TESTING_DIAGRAMS.md](TESTING_DIAGRAMS.md)**
  - 🎯 Arquitectura Mock vs Neo4j
  - 🔄 Flujo de ingesta completo
  - 🧪 Flow de testing
  - 🧩 VersionContext journey
  - 📊 Estado de componentes

---

## 🎓 Ruta de Aprendizaje Recomendada

### **Nivel 1: Beginner** (Solo quiero que funcione)
1. [QUICKSTART_TESTING.md](../QUICKSTART_TESTING.md)
2. Ejecutar `test-versioning.ps1`
3. ¡Listo!

### **Nivel 2: Intermediate** (Quiero entender qué hace)
1. [TESTING_SUMMARY.md](TESTING_SUMMARY.md)
2. [TESTING_DIAGRAMS.md](TESTING_DIAGRAMS.md) - Sección "Flujo de Ingesta"
3. [TESTING_WITH_MOCKS.md](TESTING_WITH_MOCKS.md) - Tests 1-4
4. Ejecutar tests y revisar logs

### **Nivel 3: Advanced** (Necesito debuggear o extender)
1. [TESTING_SUMMARY.md](TESTING_SUMMARY.md) - Sección "Estado Actual de Mocks"
2. [TESTING_COMMANDS.md](TESTING_COMMANDS.md) - Todos los comandos
3. [TESTING_DIAGRAMS.md](TESTING_DIAGRAMS.md) - VersionContext Flow
4. Código fuente: `MockGraphStore.cs`, `MockVectorIndex.cs`, `Program.cs`
5. Tests personalizados con comandos individuales

---

## 📂 Archivos Creados

### **Documentación**
```
docs/
├── TESTING_SUMMARY.md          # Resumen ejecutivo
├── TESTING_WITH_MOCKS.md       # Guía completa
├── TESTING_COMMANDS.md         # Comandos PowerShell
├── TESTING_DIAGRAMS.md         # Visualizaciones
└── TESTING_INDEX.md            # Este archivo
```

### **Scripts**
```
test-versioning.ps1             # Script de testing automatizado
```

### **Configuración**
```
AriadnaKnowledgeStore.Functions/
└── local.settings.json.example # Ejemplo de configuración
```

### **Guía Principal**
```
QUICKSTART_TESTING.md           # Inicio rápido (raíz)
```

---

## 🎯 Tests Disponibles

### **Tests Automatizados** (`test-versioning.ps1`)
1. ✅ **Test 1:** Primera Ingesta - VersionContext
2. ✅ **Test 2:** Segunda Ingesta - Versiones Únicas
3. ✅ **Test 3:** Verificación de Campos
4. ✅ **Test 4:** Múltiples Repos
5. ✅ **Test 5:** Verificación Manual de Logs

### **Tests Manuales** (en `TESTING_COMMANDS.md`)
- Single Ingest Request
- Comparar Dos Versiones
- Mismo Repo, Múltiples Commits
- Múltiples Repos Simultáneos (paralelismo)
- Verificar Performance
- Neo4j Queries (después de cambiar a Neo4j)

---

## 🔄 Flujo Recomendado: Mock → Neo4j

### **Fase 1: Testing con Mocks** 🟢
1. Leer [QUICKSTART_TESTING.md](../QUICKSTART_TESTING.md)
2. Configurar `local.settings.json` con `Type: "Mock"`
3. Ejecutar `test-versioning.ps1`
4. ✅ Verificar que TODOS los tests pasen
5. ✅ Verificar logs: `[MOCK]` y VersionContext consistente

### **Fase 2: Transición a Neo4j** 🔵
1. Leer [TESTING_SUMMARY.md](TESTING_SUMMARY.md) - Sección "Después del Testing"
2. Cambiar `Type: "Neo4jVersioned"` en `local.settings.json`
3. Configurar `Neo4j:Password`
4. Reiniciar Azure Function
5. Re-ejecutar `test-versioning.ps1`
6. Verificar en Neo4j Browser con queries de [TESTING_COMMANDS.md](TESTING_COMMANDS.md)

---

## ❓ FAQ

### **P: ¿Qué código necesito modificar para usar Mocks?**
**R:** Ninguno. Solo cambia la configuración en `local.settings.json`.

### **P: ¿Los Mocks validan el VersionContext correctamente?**
**R:** Sí. MockGraphStore genera VersionContext y MockVectorIndex lo recibe. Mismo flujo que Neo4j.

### **P: ¿Cómo sé que funciona?**
**R:** Ejecuta `test-versioning.ps1`. Si todos los tests pasan ✅, funciona.

### **P: ¿Puedo debuggear paso a paso?**
**R:** Sí. Usa comandos individuales de [TESTING_COMMANDS.md](TESTING_COMMANDS.md) y logs detallados.

### **P: ¿Cómo cambio de Mock a Neo4j?**
**R:** Cambia `GraphStore:Type` de `"Mock"` a `"Neo4jVersioned"` en `local.settings.json`.

### **P: ¿Los Mocks tocan Neo4j?**
**R:** No. MockGraphStore y MockVectorIndex solo usan memoria. Neo4j no es afectado.

### **P: ¿Necesito Azure OpenAI para testing con Mocks?**
**R:** No. Si `AzureOpenAI:Endpoint` está vacío, usa MockEmbeddingService automáticamente.

### **P: ¿Qué validan los tests?**
**R:** 
- ✅ VersionContext generado correctamente
- ✅ Versiones únicas por ingesta
- ✅ Campos completos en response
- ✅ Consistencia Graph → Vector
- ✅ Sin errores de compilación/runtime

---

## 🐛 Troubleshooting

| Problema | Documento | Sección |
|----------|-----------|---------|
| Error "GitHub:Token missing" | [TESTING_WITH_MOCKS.md](TESTING_WITH_MOCKS.md) | Troubleshooting |
| Tests fallan | [TESTING_COMMANDS.md](TESTING_COMMANDS.md) | Debugging |
| No se ven logs | [TESTING_WITH_MOCKS.md](TESTING_WITH_MOCKS.md) | Troubleshooting |
| Puerto ocupado | [TESTING_WITH_MOCKS.md](TESTING_WITH_MOCKS.md) | Troubleshooting |
| VersionId ausente | [TESTING_COMMANDS.md](TESTING_COMMANDS.md) | Verificar Campo |

---

## 🔗 Referencias Relacionadas

### **Documentación de Sistema**
- [VECTOR_VERSIONING_IMPLEMENTATION_COMPLETE.md](VECTOR_VERSIONING_IMPLEMENTATION_COMPLETE.md) - Implementación actual
- [VECTOR_VERSIONING_FIX_PLAN.md](VECTOR_VERSIONING_FIX_PLAN.md) - Plan aprobado
- [VERSIONING_SYSTEM_EXPLAINED.md](VERSIONING_SYSTEM_EXPLAINED.md) - Sistema de versionado

### **Diagramas Técnicos**
- [VERSIONING_DIAGRAMS.md](VERSIONING_DIAGRAMS.md) - Diagramas del sistema
- [VECTOR_VERSIONING_DIAGRAM.md](VECTOR_VERSIONING_DIAGRAM.md) - Fix visual

### **Código Fuente Relevante**
```
AriadnaKnowledgeStore.Functions/
├── Program.cs                              # DI y OrchestratOr
└── Mocks/
    ├── MockGraphStore.cs                   # Mock graph
    ├── MockVectorIndex.cs                  # Mock vector
    └── MockEmbeddingService.cs             # Mock embeddings

AriadnaKnowledgeStore.Core/
├── Abstractions.cs                         # Interfaces
└── Models.cs                               # VersionContext

AriadnaKnowledgeStore.Graph/
├── Neo4jVersionedGraphStore.cs            # Implementación real
└── Neo4jVectorIndex.cs                    # Implementación real
```

---

## 📞 Siguientes Pasos

### **1. Para Testing Inmediato:**
```powershell
code QUICKSTART_TESTING.md
```

### **2. Para Entendimiento Completo:**
```powershell
code docs\TESTING_SUMMARY.md
code docs\TESTING_DIAGRAMS.md
```

### **3. Para Debugging Avanzado:**
```powershell
code docs\TESTING_COMMANDS.md
```

### **4. Para Ejecutar Tests:**
```powershell
.\test-versioning.ps1
```

---

## ✅ Checklist Pre-Testing

```
□ He leído QUICKSTART_TESTING.md
□ Tengo local.settings.json configurado
□ GraphStore:Type = "Mock"
□ GitHub:Token configurado
□ Azure Function inicia sin errores
□ Entiendo qué validan los tests
□ Sé cómo cambiar a Neo4j después
```

---

## 🎉 Estado del Proyecto

| Componente | Estado | Documentación |
|------------|--------|---------------|
| 🟢 Mocks | ✅ Listos | [TESTING_SUMMARY.md](TESTING_SUMMARY.md) |
| 🔵 Neo4j | ✅ Listo | [VECTOR_VERSIONING_IMPLEMENTATION_COMPLETE.md](VECTOR_VERSIONING_IMPLEMENTATION_COMPLETE.md) |
| 🧪 Tests | ✅ Listos | [TESTING_WITH_MOCKS.md](TESTING_WITH_MOCKS.md) |
| 📚 Docs | ✅ Completas | Este archivo |
| 🔧 Scripts | ✅ Listos | `test-versioning.ps1` |
| 🏗️ Compilación | ✅ Exitosa | Build OK |

---

**Todo listo para testing!** 🚀

---

**Creado:** 2024  
**Propósito:** Índice de navegación para documentación de testing  
**Mantenimiento:** Actualizar cuando se agreguen nuevos documentos
