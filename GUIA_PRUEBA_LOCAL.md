# 🧪 Guía de Prueba Local - JadeiteSocialNetwork

Esta guía te ayudará a probar localmente la ingesta del repositorio JadeiteSocialNetwork y verificar los nodos creados en Neo4j.

---

## ✅ Pre-requisitos

Antes de empezar, verifica que tienes:

1. ✅ **Azure Functions Core Tools** instalado
   ```powershell
   func --version  # Debe mostrar 4.x
   ```

2. ✅ **Neo4j AuraDB** accesible
   - URL: https://console.neo4j.io/
   - Tu instancia debe estar "Running"

3. ✅ **GitHub Token** configurado en `local.settings.json`
   - Debe tener permisos de `repo` (lectura)

4. ✅ **Configuración verificada**
   ```powershell
   # Verificar que local.settings.json tiene las credenciales correctas
   Get-Content CodeIntel.Functions/local.settings.json
   ```

---

## 🚀 Paso 1: Iniciar Azure Functions Localmente

### 1.1 Abrir terminal en la carpeta del proyecto

```powershell
cd C:\proyectos\gh-code-intel-mvp\src
```

### 1.2 Navegar a CodeIntel.Functions

```powershell
cd CodeIntel.Functions
```

### 1.3 Iniciar Azure Functions

```powershell
func start
```

**Espera a ver este mensaje:**
```
Functions:
  GitHubWebhook: [POST] http://localhost:7071/api/webhook/github
  GetVersionHistory: [GET] http://localhost:7071/api/repo/{owner}/{repo}/{branch}/versions
  ingest: [POST] http://localhost:7071/api/ingest
  ...

Host initialized (XXXms)
Host started (XXXms)
```

**⚠️ Deja esta terminal abierta** - las Azure Functions deben estar corriendo.

---

## 🧪 Paso 2: Ejecutar Ingesta (en otra terminal)

### 2.1 Abrir NUEVA terminal PowerShell

### 2.2 Navegar a la carpeta de scripts

```powershell
cd C:\proyectos\gh-code-intel-mvp\src\scripts
```

### 2.3 Ejecutar script de ingesta

```powershell
.\Test-Ingest-JadeiteSocialNetwork.ps1
```

**Qué hace este script:**
1. Verifica que Azure Functions esté corriendo
2. Llama al endpoint `/api/ingest` con el payload:
   ```json
   {
     "owner": "jesusinfantes-hkteck",
     "repo": "JadeiteSocialNetwork",
     "branch": "master"
   }
   ```
3. Espera la respuesta (2-5 minutos dependiendo del tamaño del repo)
4. Muestra el resumen de nodos creados
5. Imprime queries útiles para Neo4j

**Salida esperada:**
```
========================================
CodeIntel - Test Ingesta Local
Repositorio: JadeiteSocialNetwork
========================================

📋 Configuración:
   Owner:  jesusinfantes-hkteck
   Repo:   JadeiteSocialNetwork
   Branch: master
   URL:    http://localhost:7071/api/ingest

🔍 Verificando que Azure Functions esté corriendo...
✅ Azure Functions está corriendo

🚀 Iniciando ingesta...
⏳ Procesando repositorio (esto puede tardar 2-5 minutos)...
   - Descargando código de GitHub
   - Analizando archivos .cs y .aspx
   - Creando nodos y relaciones en Neo4j
   - Generando embeddings (si está configurado)

✅ ¡Ingesta completada exitosamente!
⏱️  Duración: 87.34 segundos

📊 Resultados:
{
  "classes": 45,
  "methods": 238,
  "aspxPages": 12,
  "aspxControls": 67,
  "aspxEvents": 89,
  "edges": 412
}

📈 Resumen de nodos creados:
   Clases:        45
   Métodos:       238
   Páginas ASPX:  12
   Controles:     67
   Eventos ASPX:  89
   Relaciones:    412
```

---

## 🔍 Paso 3: Verificar en Neo4j Browser

### 3.1 Abrir Neo4j Console

Ir a: https://console.neo4j.io/projects/2a4895fc-e83c-4a1e-987f-f918237f8667/tools/explore

### 3.2 Ejecutar queries de verificación

El script imprimió varias queries útiles. Aquí las más importantes:

#### Query 1: Ver si el repositorio fue creado

```cypher
MATCH (r:Repository {id: "jesusinfantes-hkteck/JadeiteSocialNetwork@master"})
RETURN r
```

Deberías ver 1 nodo `Repository`.

#### Query 2: Contar nodos creados

```cypher
MATCH (c:Class {repoId: "jesusinfantes-hkteck/JadeiteSocialNetwork@master"})
WITH count(c) AS totalClasses
MATCH (m:Method {repoId: "jesusinfantes-hkteck/JadeiteSocialNetwork@master"})
WITH totalClasses, count(m) AS totalMethods
MATCH (p:AspxPage {repoId: "jesusinfantes-hkteck/JadeiteSocialNetwork@master"})
WITH totalClasses, totalMethods, count(p) AS totalPages
MATCH (ctrl:AspxControl {repoId: "jesusinfantes-hkteck/JadeiteSocialNetwork@master"})
WITH totalClasses, totalMethods, totalPages, count(ctrl) AS totalControls
MATCH (e:AspxEvent {repoId: "jesusinfantes-hkteck/JadeiteSocialNetwork@master"})
RETURN 
  totalClasses AS `Clases`,
  totalMethods AS `Métodos`,
  totalPages AS `Páginas ASPX`,
  totalControls AS `Controles`,
  count(e) AS `Eventos ASPX`
```

#### Query 3: Ver primeras clases

```cypher
MATCH (c:Class {repoId: "jesusinfantes-hkteck/JadeiteSocialNetwork@master"})
RETURN c.name AS Clase,
       c.namespace AS Namespace,
       c.filePath AS Archivo
ORDER BY c.name
LIMIT 20
```

#### Query 4: Visualizar el grafo

**Cambia a vista "Graph" en Neo4j Browser**, luego ejecuta:

```cypher
MATCH (n {repoId: "jesusinfantes-hkteck/JadeiteSocialNetwork@master"})
RETURN n
LIMIT 50
```

Deberías ver un grafo con:
- 🟦 Nodos azules: `Class`
- 🟩 Nodos verdes: `Method`
- 🟨 Nodos amarillos: `AspxPage` (si el repo tiene ASPX)
- 🟧 Nodos naranjas: `AspxControl`
- Flechas mostrando relaciones: `BELONGS_TO`, `CALLS`, `DEPENDS_ON`, etc.

---

## 📋 Paso 4: Explorar con Queries Avanzadas (Opcional)

### Script de verificación completo

Para obtener todas las queries de verificación pre-generadas:

```powershell
cd C:\proyectos\gh-code-intel-mvp\src\scripts
.\Verify-Neo4j-Data.ps1
```

Este script imprime queries para:
- ✅ Verificar repositorio y versiones
- ✅ Contar nodos
- ✅ Explorar clases y métodos
- ✅ Ver páginas ASPX y controles
- ✅ Analizar dependencias
- ✅ Visualizar grafo
- ✅ Queries temporales (versionado)

---

## 🐛 Troubleshooting

### Error: "Azure Functions no está corriendo"

**Solución:**
1. Abre otra terminal
2. Ejecuta:
   ```powershell
   cd CodeIntel.Functions
   func start
   ```
3. Espera a que inicie completamente
4. Vuelve a ejecutar el script de ingesta

### Error: "Token de GitHub inválido"

**Solución:**
1. Verifica tu token en: https://github.com/settings/tokens
2. Asegúrate que tiene permisos `repo`
3. Actualiza `CodeIntel.Functions/local.settings.json`:
   ```json
   "GitHub:Token": "ghp_TU_TOKEN_NUEVO"
   ```
4. Reinicia Azure Functions (`Ctrl+C` y `func start`)

### Error: "Neo4j no está accesible"

**Solución:**
1. Ve a: https://console.neo4j.io/
2. Verifica que tu instancia esté "Running" (verde)
3. Si está "Paused", haz clic en "Resume"
4. Verifica las credenciales en `local.settings.json`

### Timeout después de 10 minutos

**Causa:** El repositorio es muy grande.

**Solución:**
1. Edita `scripts/Test-Ingest-JadeiteSocialNetwork.ps1`
2. Cambia `-TimeoutSec 600` a `-TimeoutSec 1800` (30 minutos)
3. Vuelve a ejecutar

### No se crearon nodos ASPX

**Normal:** Si el repositorio no tiene archivos `.aspx` o `.ascx`, los contadores de ASPX serán 0.

---

## ✅ Resultado Esperado

Al final deberías tener:

1. ✅ Un nodo `Repository` en Neo4j
2. ✅ Un nodo `Version` (la versión actual)
3. ✅ Nodos `Class` con todas las clases del repo
4. ✅ Nodos `Method` con todos los métodos
5. ✅ Relaciones `BELONGS_TO`, `CALLS`, `DEPENDS_ON`, etc.
6. ✅ (Si hay ASPX) Nodos `AspxPage`, `AspxControl`, `AspxEvent`
7. ✅ Visualización completa del grafo en Neo4j Browser

---

## 🎯 Próximos Pasos

Después de verificar que funciona localmente:

1. **Explorar el grafo** con queries más específicas
2. **Hacer cambios** al repositorio en GitHub
3. **Re-ingerir** para crear una nueva versión
4. **Probar rollback** a versiones anteriores
5. **Configurar webhook** en GitHub para ingesta automática

---

## 📚 Referencias

- [README.md](../README.md) - Documentación principal
- [CONFIGURACION_NEO4J_AURADB.md](../CONFIGURACION_NEO4J_AURADB.md) - Setup de Neo4j Cloud
- [docs/Guia_Uso_Versionado.md](../docs/Guia_Uso_Versionado.md) - Guía de versionado
- [ASPX_QUERY_EXAMPLES.md](../ASPX_QUERY_EXAMPLES.md) - Queries para ASPX
