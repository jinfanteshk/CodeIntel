# 🔷 Blazor Components - Neo4j Query Examples

Este documento contiene ejemplos de queries Cypher para consultar componentes Blazor en el grafo de conocimiento.

---

## 📋 **Estructura de Nodos Blazor**

### **Tipos de Nodos:**

1. **BlazorComponent** - Componente Blazor principal
2. **BlazorParameter** - Parámetro del componente
3. **BlazorEventCallback** - Event callback del componente
4. **BlazorChildComponent** - Uso de componente hijo

---

## 🔍 **Queries Básicas**

### **1. Listar todos los componentes Blazor**

```cypher
MATCH (bc:BlazorComponent)
WHERE bc.validTo IS NULL
RETURN bc.name, bc.filePath, bc.pageRoute
ORDER BY bc.name
```

### **2. Encontrar componentes con rutas (@page)**

```cypher
MATCH (bc:BlazorComponent)
WHERE bc.validTo IS NULL 
  AND bc.pageRoute <> ""
RETURN bc.name, bc.pageRoute, bc.filePath
ORDER BY bc.pageRoute
```

### **3. Componentes que inyectan un servicio específico**

```cypher
MATCH (bc:BlazorComponent)
WHERE bc.validTo IS NULL 
  AND bc.injectedServices CONTAINS "IPaymentService"
RETURN bc.name, bc.filePath, bc.pageRoute
```

### **4. Ver todos los parámetros de un componente**

```cypher
MATCH (bc:BlazorComponent {name: "ProductCard"})
WHERE bc.validTo IS NULL
MATCH (bc)-[:HAS_PARAMETER]->(p:BlazorParameter)
WHERE p.validTo IS NULL
RETURN p.name, p.type, p.isRequired
```

### **5. Ver todos los event callbacks de un componente**

```cypher
MATCH (bc:BlazorComponent {name: "ProductCard"})
WHERE bc.validTo IS NULL
MATCH (bc)-[:HAS_EVENT_CALLBACK]->(ec:BlazorEventCallback)
WHERE ec.validTo IS NULL
RETURN ec.name, ec.eventType
```

---

## 🔗 **Queries de Dependencias**

### **6. Ver todas las dependencias de un componente**

```cypher
MATCH (bc:BlazorComponent {name: "Checkout"})
WHERE bc.validTo IS NULL
OPTIONAL MATCH (bc)-[:HAS_PARAMETER]->(param:BlazorParameter)
WHERE param.validTo IS NULL
OPTIONAL MATCH (bc)-[:HAS_EVENT_CALLBACK]->(callback:BlazorEventCallback)
WHERE callback.validTo IS NULL
OPTIONAL MATCH (bc)-[:USES_CHILD_COMPONENT]->(child)
RETURN bc.name, 
       bc.pageRoute,
       collect(DISTINCT param.name) as parameters,
       collect(DISTINCT callback.name) as callbacks,
       collect(DISTINCT child) as childComponents,
       split(bc.injectedServices, ',') as services
```

### **7. Encontrar componentes que usan un servicio específico**

```cypher
MATCH (bc:BlazorComponent)
WHERE bc.validTo IS NULL 
  AND bc.injectedServices CONTAINS "IProductService"
RETURN bc.name, bc.filePath, bc.pageRoute
ORDER BY bc.name
```

### **8. Encontrar componentes hijos usados por un componente**

```cypher
MATCH (parent:BlazorComponent {name: "ProductList"})
WHERE parent.validTo IS NULL
MATCH (parent)-[:USES_CHILD_COMPONENT]->(child:BlazorChildComponent)
WHERE child.validTo IS NULL
RETURN parent.name, child.childComponentName
```

### **9. Encontrar todos los padres de un componente hijo**

```cypher
MATCH (parent:BlazorComponent)-[:USES_CHILD_COMPONENT]->(child:BlazorChildComponent)
WHERE parent.validTo IS NULL 
  AND child.validTo IS NULL
  AND child.childComponentName = "ProductCard"
RETURN parent.name, parent.filePath, parent.pageRoute
```

---

## 🔄 **Queries de Análisis de Impacto**

### **10. Componentes que comparten el mismo servicio**

```cypher
MATCH (bc1:BlazorComponent)
WHERE bc1.validTo IS NULL 
  AND bc1.injectedServices CONTAINS "IPaymentService"
MATCH (bc2:BlazorComponent)
WHERE bc2.validTo IS NULL 
  AND bc2.injectedServices CONTAINS "IPaymentService"
  AND bc1.name <> bc2.name
RETURN DISTINCT bc1.name, bc2.name, 
       bc1.filePath as file1, 
       bc2.filePath as file2
```

### **11. Impacto de cambio en un componente (quién lo usa)**

```cypher
MATCH (child:BlazorChildComponent {childComponentName: "ProductCard"})
WHERE child.validTo IS NULL
MATCH (parent:BlazorComponent)-[:USES_CHILD_COMPONENT]->(child)
WHERE parent.validTo IS NULL
RETURN parent.name, parent.filePath, parent.pageRoute
ORDER BY parent.name
```

### **12. Componentes con parámetros requeridos**

```cypher
MATCH (bc:BlazorComponent)
WHERE bc.validTo IS NULL
MATCH (bc)-[:HAS_PARAMETER]->(p:BlazorParameter {isRequired: true})
WHERE p.validTo IS NULL
RETURN bc.name, collect(p.name) as requiredParameters, bc.filePath
ORDER BY bc.name
```

---

## 🔍 **Queries de Búsqueda Avanzada**

### **13. Componentes que heredan de una clase base específica**

```cypher
MATCH (bc:BlazorComponent)
WHERE bc.validTo IS NULL 
  AND bc.baseType = "OwningComponentBase"
RETURN bc.name, bc.filePath, bc.pageRoute
```

### **14. Páginas Blazor (componentes con @page) agrupadas por ruta**

```cypher
MATCH (bc:BlazorComponent)
WHERE bc.validTo IS NULL 
  AND bc.pageRoute <> ""
RETURN bc.pageRoute, collect(bc.name) as components
ORDER BY bc.pageRoute
```

### **15. Componentes con más de N parámetros**

```cypher
MATCH (bc:BlazorComponent)
WHERE bc.validTo IS NULL
OPTIONAL MATCH (bc)-[:HAS_PARAMETER]->(p:BlazorParameter)
WHERE p.validTo IS NULL
WITH bc, count(p) as paramCount
WHERE paramCount > 3
RETURN bc.name, bc.filePath, paramCount
ORDER BY paramCount DESC
```

### **16. Componentes que usan múltiples servicios**

```cypher
MATCH (bc:BlazorComponent)
WHERE bc.validTo IS NULL 
  AND bc.injectedServices <> ""
WITH bc, size(split(bc.injectedServices, ',')) as serviceCount
WHERE serviceCount > 2
RETURN bc.name, bc.filePath, serviceCount, split(bc.injectedServices, ',') as services
ORDER BY serviceCount DESC
```

---

## 📊 **Queries de Estadísticas**

### **17. Contar componentes por tipo**

```cypher
MATCH (bc:BlazorComponent)
WHERE bc.validTo IS NULL
WITH bc, 
     CASE WHEN bc.pageRoute <> "" THEN "Page" ELSE "Component" END as type
RETURN type, count(bc) as count
ORDER BY count DESC
```

### **18. Top servicios más inyectados**

```cypher
MATCH (bc:BlazorComponent)
WHERE bc.validTo IS NULL 
  AND bc.injectedServices <> ""
UNWIND split(bc.injectedServices, ',') as service
RETURN service, count(*) as usage
ORDER BY usage DESC
LIMIT 10
```

### **19. Componentes más reutilizados (más padres)**

```cypher
MATCH (child:BlazorChildComponent)
WHERE child.validTo IS NULL
WITH child.childComponentName as componentName, count(*) as usageCount
WHERE usageCount > 1
RETURN componentName, usageCount
ORDER BY usageCount DESC
```

---

## 🔀 **Queries Cross-Technology**

### **20. Componentes Blazor que usan el mismo servicio que clases C#**

```cypher
MATCH (bc:BlazorComponent)
WHERE bc.validTo IS NULL 
  AND bc.injectedServices CONTAINS "IProductService"
WITH collect(bc.name) as blazorComponents
MATCH (m:Method)
WHERE m.validTo IS NULL 
  AND m.body CONTAINS "IProductService"
MATCH (c:Class)-[:HAS_METHOD]->(m)
WHERE c.validTo IS NULL
RETURN blazorComponents, collect(DISTINCT c.name) as csharpClasses
```

### **21. Encontrar código relacionado con Payment en todos los tipos**

```cypher
// Componentes Blazor
MATCH (bc:BlazorComponent)
WHERE bc.validTo IS NULL 
  AND (bc.name CONTAINS "Payment" OR bc.injectedServices CONTAINS "Payment")
WITH collect({type: "BlazorComponent", name: bc.name, path: bc.filePath}) as blazorResults

// Clases C#
MATCH (c:Class)
WHERE c.validTo IS NULL 
  AND c.name CONTAINS "Payment"
WITH blazorResults, collect({type: "Class", name: c.name, path: c.filePath}) as classResults

// Métodos C#
MATCH (m:Method)
WHERE m.validTo IS NULL 
  AND (m.name CONTAINS "Payment" OR m.body CONTAINS "Payment")
MATCH (c:Class)-[:HAS_METHOD]->(m)
WHERE c.validTo IS NULL
WITH blazorResults, classResults, 
     collect({type: "Method", name: m.name, class: c.name, path: m.filePath}) as methodResults

RETURN blazorResults + classResults + methodResults as allResults
```

---

## 🕐 **Queries de Versionado**

### **22. Ver historial de cambios de un componente**

```cypher
MATCH (bc:BlazorComponent {name: "ProductCard"})
WHERE bc.id CONTAINS "ProductCard"
OPTIONAL MATCH (bc)-[:NEXT_VERSION*]->(newer:BlazorComponent)
OPTIONAL MATCH (older:BlazorComponent)-[:NEXT_VERSION*]->(bc)
RETURN older, bc, newer
ORDER BY bc.validFrom DESC
```

### **23. Ver componentes modificados en una versión específica**

```cypher
MATCH (v:Version {id: "version-guid-here"})-[:CONTAINS]->(bc:BlazorComponent)
RETURN bc.name, bc.filePath, bc.pageRoute
ORDER BY bc.name
```

### **24. Comparar servicios inyectados entre versiones**

```cypher
MATCH (old:BlazorComponent {name: "Checkout"})-[:NEXT_VERSION]->(new:BlazorComponent)
WHERE old.injectedServices <> new.injectedServices
RETURN old.injectedServices as oldServices,
       new.injectedServices as newServices,
       old.validFrom as oldVersion,
       new.validFrom as newVersion
```

---

## 🎯 **Query GraphRAG: Ejemplo Completo**

### **25. Pipeline completo: Vector Search → Graph Expansion**

```cypher
// Paso 1: Asumir que Vector Search devolvió este componente
MATCH (bc:BlazorComponent {id: "blazor:/Pages/Checkout.razor"})
WHERE bc.validTo IS NULL

// Paso 2: Expandir el grafo completo
OPTIONAL MATCH (bc)-[:HAS_PARAMETER]->(param:BlazorParameter)
WHERE param.validTo IS NULL

OPTIONAL MATCH (bc)-[:HAS_EVENT_CALLBACK]->(callback:BlazorEventCallback)
WHERE callback.validTo IS NULL

OPTIONAL MATCH (bc)-[:USES_CHILD_COMPONENT]->(child:BlazorChildComponent)
WHERE child.validTo IS NULL

// Paso 3: Encontrar componentes similares (mismo servicio)
WITH bc, param, callback, child
WHERE bc.injectedServices CONTAINS "IPaymentService"
MATCH (similar:BlazorComponent)
WHERE similar.validTo IS NULL 
  AND similar.injectedServices CONTAINS "IPaymentService"
  AND similar <> bc

// Paso 4: Ver quién usa este componente
OPTIONAL MATCH (parent)-[:USES_CHILD_COMPONENT]->(selfChild:BlazorChildComponent)
WHERE selfChild.childComponentName = bc.name
  AND parent.validTo IS NULL
  AND selfChild.validTo IS NULL

// Paso 5: Retornar contexto completo
RETURN bc.name as component,
       bc.pageRoute as route,
       split(bc.injectedServices, ',') as services,
       collect(DISTINCT param.name) as parameters,
       collect(DISTINCT callback.name) as callbacks,
       collect(DISTINCT child.childComponentName) as childComponents,
       collect(DISTINCT similar.name) as similarComponents,
       collect(DISTINCT parent.name) as usedBy
```

---

## 📝 **Notas de Implementación**

### **Propiedades de Nodos:**

**BlazorComponent:**
- `id`: Identificador único (ej: "blazor:/Components/ProductCard.razor")
- `name`: Nombre del componente
- `filePath`: Ruta relativa del archivo
- `pageRoute`: Ruta de @page (vacío si no es página)
- `baseType`: Tipo base de @inherits (vacío si usa ComponentBase)
- `injectedServices`: Lista separada por comas de servicios inyectados
- `codeBlock`: Contenido del bloque @code
- `versionId`: ID de versión para versionado temporal
- `validFrom`, `validTo`: Timestamps para versionado

**BlazorParameter:**
- `id`, `componentId`, `name`, `type`, `isRequired`
- `versionId`, `validFrom`, `validTo`

**BlazorEventCallback:**
- `id`, `componentId`, `name`, `eventType`
- `versionId`, `validFrom`, `validTo`

**BlazorChildComponent:**
- `id`, `parentComponentId`, `childComponentName`
- `versionId`, `validFrom`, `validTo`

### **Edges (Relaciones):**

- `HAS_PARAMETER`: BlazorComponent → BlazorParameter
- `HAS_EVENT_CALLBACK`: BlazorComponent → BlazorEventCallback
- `USES_CHILD_COMPONENT`: BlazorComponent → BlazorChildComponent
- `USES_SERVICE`: BlazorComponent → Service (virtual node)
- `INHERITS_COMPONENT`: BlazorComponent → Base Component
- `ROUTES_TO_COMPONENT`: Route → BlazorComponent
- `NEXT_VERSION`: BlazorComponent → BlazorComponent (versionado)

---

## 🚀 **Uso en GraphRAG**

Para implementar GraphRAG completo:

1. **Vector Search** encuentra el chunk más relevante
2. Extraer el `id` del nodo desde el chunk
3. Ejecutar query de expansión de grafo (query #25)
4. Combinar resultados vectoriales + contexto de grafo
5. Enviar a LLM para generar respuesta enriquecida

---

**Última actualización:** 2024
**Versión:** 1.0
