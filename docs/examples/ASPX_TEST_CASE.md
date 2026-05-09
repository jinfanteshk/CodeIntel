# Caso de Prueba: Análisis ASPX Legacy

Este documento muestra un ejemplo completo del análisis de una aplicación legacy WebForms.

## 📁 Estructura del Repositorio de Prueba

```
LegacyWebApp/
├── Default.aspx
├── Default.aspx.cs
├── Login.aspx
├── Login.aspx.cs
├── UserProfile.aspx
├── UserProfile.aspx.cs
├── Controls/
│   ├── Header.ascx
│   ├── Header.ascx.cs
│   ├── Footer.ascx
│   └── Footer.ascx.cs
└── App_Code/
    ├── DataAccess.cs
    └── BusinessLogic.cs
```

## 📄 Ejemplo de Archivo ASPX

### Default.aspx
```aspx
<%@ Page Language="C#" AutoEventWireup="true" 
    CodeBehind="Default.aspx.cs" 
    Inherits="LegacyWebApp.DefaultPage" %>

<!DOCTYPE html>
<html>
<head>
    <title>Welcome</title>
</head>
<body>
    <form id="form1" runat="server">
        <h1>Welcome to Legacy App</h1>

        <asp:Label ID="lblWelcome" runat="server" Text="Hello"></asp:Label>

        <asp:TextBox ID="txtName" runat="server" 
                     OnTextChanged="txtName_TextChanged" 
                     AutoPostBack="true"></asp:TextBox>

        <asp:Button ID="btnSubmit" runat="server" 
                    Text="Submit" 
                    OnClick="btnSubmit_Click"></asp:Button>

        <asp:GridView ID="gvUsers" runat="server" 
                      AutoGenerateColumns="false"
                      OnRowDataBound="gvUsers_RowDataBound"
                      OnSelectedIndexChanged="gvUsers_SelectedIndexChanged">
            <Columns>
                <asp:BoundField DataField="Name" HeaderText="Name" />
                <asp:BoundField DataField="Email" HeaderText="Email" />
            </Columns>
        </asp:GridView>
    </form>
</body>
</html>
```

### Default.aspx.cs
```csharp
using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using LegacyWebApp.AppCode;

namespace LegacyWebApp
{
    public partial class DefaultPage : Page
    {
        private DataAccess _dataAccess = new DataAccess();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                LoadUsers();
            }
        }

        protected void txtName_TextChanged(object sender, EventArgs e)
        {
            lblWelcome.Text = $"Hello, {txtName.Text}!";
        }

        protected void btnSubmit_Click(object sender, EventArgs e)
        {
            SaveData();
            LoadUsers();
        }

        protected void gvUsers_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                // Custom formatting
            }
        }

        protected void gvUsers_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedRow = gvUsers.SelectedRow;
            // Handle selection
        }

        private void LoadUsers()
        {
            var users = _dataAccess.GetAllUsers();
            gvUsers.DataSource = users;
            gvUsers.DataBind();
        }

        private void SaveData()
        {
            _dataAccess.SaveUser(txtName.Text);
        }
    }
}
```

## 🎯 Resultado del Análisis

### Nodos Creados

#### AspxPage
```json
{
  "id": "aspx:Default.aspx",
  "name": "Default.aspx",
  "filePath": "Default.aspx",
  "codeBehindClass": "LegacyWebApp.DefaultPage",
  "inherits": "LegacyWebApp.DefaultPage"
}
```

#### AspxControls (4 controles)
```json
[
  {
    "id": "control:aspx:Default.aspx#lblWelcome",
    "name": "lblWelcome",
    "type": "asp:Label",
    "pageId": "aspx:Default.aspx",
    "filePath": "Default.aspx",
    "events": null
  },
  {
    "id": "control:aspx:Default.aspx#txtName",
    "name": "txtName",
    "type": "asp:TextBox",
    "pageId": "aspx:Default.aspx",
    "filePath": "Default.aspx",
    "events": {
      "OnTextChanged": "txtName_TextChanged"
    }
  },
  {
    "id": "control:aspx:Default.aspx#btnSubmit",
    "name": "btnSubmit",
    "type": "asp:Button",
    "pageId": "aspx:Default.aspx",
    "filePath": "Default.aspx",
    "events": {
      "OnClick": "btnSubmit_Click"
    }
  },
  {
    "id": "control:aspx:Default.aspx#gvUsers",
    "name": "gvUsers",
    "type": "asp:GridView",
    "pageId": "aspx:Default.aspx",
    "filePath": "Default.aspx",
    "events": {
      "OnRowDataBound": "gvUsers_RowDataBound",
      "OnSelectedIndexChanged": "gvUsers_SelectedIndexChanged"
    }
  }
]
```

#### AspxEvents (4 eventos)
```json
[
  {
    "id": "event:control:aspx:Default.aspx#txtName#OnTextChanged",
    "eventName": "OnTextChanged",
    "controlId": "control:aspx:Default.aspx#txtName",
    "handlerMethod": "txtName_TextChanged"
  },
  {
    "id": "event:control:aspx:Default.aspx#btnSubmit#OnClick",
    "eventName": "OnClick",
    "controlId": "control:aspx:Default.aspx#btnSubmit",
    "handlerMethod": "btnSubmit_Click"
  },
  {
    "id": "event:control:aspx:Default.aspx#gvUsers#OnRowDataBound",
    "eventName": "OnRowDataBound",
    "controlId": "control:aspx:Default.aspx#gvUsers",
    "handlerMethod": "gvUsers_RowDataBound"
  },
  {
    "id": "event:control:aspx:Default.aspx#gvUsers#OnSelectedIndexChanged",
    "eventName": "OnSelectedIndexChanged",
    "controlId": "control:aspx:Default.aspx#gvUsers",
    "handlerMethod": "gvUsers_SelectedIndexChanged"
  }
]
```

#### CodeClass
```json
{
  "id": "class:LegacyWebApp.DefaultPage",
  "name": "DefaultPage",
  "namespace": "LegacyWebApp",
  "filePath": "Default.aspx.cs"
}
```

#### CodeMethods (7 métodos)
```json
[
  {
    "id": "method:LegacyWebApp.DefaultPage.Page_Load",
    "name": "Page_Load",
    "classId": "class:LegacyWebApp.DefaultPage"
  },
  {
    "id": "method:LegacyWebApp.DefaultPage.txtName_TextChanged",
    "name": "txtName_TextChanged",
    "classId": "class:LegacyWebApp.DefaultPage"
  },
  {
    "id": "method:LegacyWebApp.DefaultPage.btnSubmit_Click",
    "name": "btnSubmit_Click",
    "classId": "class:LegacyWebApp.DefaultPage"
  },
  {
    "id": "method:LegacyWebApp.DefaultPage.gvUsers_RowDataBound",
    "name": "gvUsers_RowDataBound",
    "classId": "class:LegacyWebApp.DefaultPage"
  },
  {
    "id": "method:LegacyWebApp.DefaultPage.gvUsers_SelectedIndexChanged",
    "name": "gvUsers_SelectedIndexChanged",
    "classId": "class:LegacyWebApp.DefaultPage"
  },
  {
    "id": "method:LegacyWebApp.DefaultPage.LoadUsers",
    "name": "LoadUsers",
    "classId": "class:LegacyWebApp.DefaultPage"
  },
  {
    "id": "method:LegacyWebApp.DefaultPage.SaveData",
    "name": "SaveData",
    "classId": "class:LegacyWebApp.DefaultPage"
  }
]
```

### Relaciones Creadas

```
(aspx:Default.aspx)-[:CODE_BEHIND]->(class:LegacyWebApp.DefaultPage)

(aspx:Default.aspx)-[:HAS_CONTROL]->(control:aspx:Default.aspx#lblWelcome)
(aspx:Default.aspx)-[:HAS_CONTROL]->(control:aspx:Default.aspx#txtName)
(aspx:Default.aspx)-[:HAS_CONTROL]->(control:aspx:Default.aspx#btnSubmit)
(aspx:Default.aspx)-[:HAS_CONTROL]->(control:aspx:Default.aspx#gvUsers)

(control:aspx:Default.aspx#txtName)-[:HAS_CONTROL]->(event:txtName#OnTextChanged)
(control:aspx:Default.aspx#btnSubmit)-[:HAS_CONTROL]->(event:btnSubmit#OnClick)
(control:aspx:Default.aspx#gvUsers)-[:HAS_CONTROL]->(event:gvUsers#OnRowDataBound)
(control:aspx:Default.aspx#gvUsers)-[:HAS_CONTROL]->(event:gvUsers#OnSelectedIndexChanged)

(event:txtName#OnTextChanged)-[:HANDLES_EVENT]->(method:txtName_TextChanged)
(event:btnSubmit#OnClick)-[:HANDLES_EVENT]->(method:btnSubmit_Click)
(event:gvUsers#OnRowDataBound)-[:HANDLES_EVENT]->(method:gvUsers_RowDataBound)
(event:gvUsers#OnSelectedIndexChanged)-[:HANDLES_EVENT]->(method:gvUsers_SelectedIndexChanged)

(method:btnSubmit_Click)-[:CALLS]->(method:SaveData)
(method:btnSubmit_Click)-[:CALLS]->(method:LoadUsers)
(method:LoadUsers)-[:CALLS]->(method:DataAccess.GetAllUsers)
(method:SaveData)-[:CALLS]->(method:DataAccess.SaveUser)
```

## 📊 Visualización del Grafo

```
                    ┌─────────────┐
                    │ DefaultPage │◄──────┐
                    │   (Class)   │       │
                    └──────┬──────┘       │
                           │              │
         ┌─────────────────┼──────────────┼───────────────┐
         │                 │              │               │
         ▼                 ▼              │               ▼
    ┌─────────┐      ┌──────────┐   CODE_BEHIND    ┌──────────┐
    │Page_Load│      │SaveData  │        │         │LoadUsers │
    └─────────┘      └────┬─────┘        │         └────┬─────┘
                          │              │              │
                          ▼              │              ▼
                   ┌────────────┐   ┌────────┐   ┌─────────────┐
                   │DataAccess. │   │Default │   │DataAccess.  │
                   │SaveUser    │   │  ASPX  │   │GetAllUsers  │
                   └────────────┘   └───┬────┘   └─────────────┘
                                        │
                        ┌───────────────┼───────────────┐
                        │               │               │
                        ▼               ▼               ▼
                   ┌─────────┐    ┌──────────┐   ┌──────────┐
                   │txtName  │    │btnSubmit │   │ gvUsers  │
                   │(TextBox)│    │ (Button) │   │(GridView)│
                   └────┬────┘    └────┬─────┘   └────┬─────┘
                        │              │              │
                        ▼              ▼              ├─────────────┐
                 OnTextChanged     OnClick           │             │
                        │              │              ▼             ▼
                        ▼              ▼       OnRowDataBound  OnSelectedIndex
                        │              │              │         Changed
                        └──────┬───────┘              │             │
                               │                      │             │
                               ▼                      ▼             ▼
                         Event Handlers          Event Handlers
```

## 🔍 Consultas de Ejemplo para Este Caso

### 1. Ver flujo completo desde botón hasta base de datos
```cypher
MATCH path = (page:AspxPage {name: 'Default.aspx'})
             -[:HAS_CONTROL]->(btn:AspxControl {name: 'btnSubmit'})
             -[:TRIGGERS]->(event:AspxEvent)
             -[:HANDLES_EVENT]->(handler:Method)
             -[:CALLS*1..3]->(called:Method)
WHERE called.name =~ '.*Data.*'
RETURN path
```

### 2. Encontrar todos los puntos de entrada interactivos
```cypher
MATCH (page:AspxPage {name: 'Default.aspx'})
      -[:HAS_CONTROL]->(control:AspxControl)
      -[:TRIGGERS]->(event:AspxEvent)
      -[:HANDLES_EVENT]->(method:Method)
RETURN control.name as Control, 
       event.eventName as Event, 
       method.name as Handler
```

### 3. Analizar dependencias de DataAccess
```cypher
MATCH (page:AspxPage {name: 'Default.aspx'})
      -[:CODE_BEHIND]->(class:Class)
      -[:HAS_METHOD]->(method:Method)
      -[:DEPENDS_ON|CALLS*1..2]->(dep)
WHERE dep.name =~ '.*DataAccess.*'
RETURN method.name, collect(DISTINCT dep.name) as Dependencies
```

## 📈 Métricas del Análisis

```json
{
  "repo": "example/legacy-webapp@main",
  "downloadedTo": "/tmp/repos/legacy-webapp",
  "classes": 3,
  "methods": 15,
  "aspxPages": 4,
  "aspxControls": 12,
  "aspxEvents": 18,
  "edges": 45,
  "chunksGenerated": 52,
  "indexed": 52
}
```

## 🎯 Insights Obtenidos

1. **Complejidad de UI**: Default.aspx tiene 4 controles interactivos con 4 eventos
2. **Acoplamiento**: `btnSubmit_Click` llama a 2 métodos privados que acceden a `DataAccess`
3. **Puntos de entrada**: 4 event handlers que los usuarios pueden activar
4. **Dependencias externas**: Clase `DataAccess` compartida entre múltiples páginas

## ✅ Validación en Neo4j

Después de ejecutar la ingesta, ejecutar estas consultas para verificar:

```cypher
// 1. Verificar página
MATCH (p:AspxPage {name: 'Default.aspx'})
RETURN p

// 2. Verificar controles
MATCH (p:AspxPage {name: 'Default.aspx'})-[:HAS_CONTROL]->(c)
RETURN count(c) // Debería ser 4

// 3. Verificar eventos
MATCH (p:AspxPage {name: 'Default.aspx'})
      -[:HAS_CONTROL]->()
      -[:TRIGGERS]->(e)
RETURN count(e) // Debería ser 4

// 4. Verificar code-behind
MATCH (p:AspxPage {name: 'Default.aspx'})
      -[:CODE_BEHIND]->(c:Class)
RETURN c.name // Debería ser 'DefaultPage'

// 5. Verificar event handlers
MATCH (e:AspxEvent {handlerMethod: 'btnSubmit_Click'})
      -[:HANDLES_EVENT]->(m:Method)
RETURN m.name // Debería ser 'btnSubmit_Click'
```

## 🚀 Próximos Pasos con Estos Datos

1. **Búsqueda Semántica**: "Muéstrame páginas con formularios de envío"
2. **Análisis de Impacto**: "¿Qué páginas se verían afectadas si cambio DataAccess?"
3. **Detección de Patrones**: "Encuentra páginas con el patrón CRUD completo"
4. **Métricas de Calidad**: "¿Cuáles son las páginas más complejas para priorizar refactoring?"
5. **Asistente de Migración**: "Genera un plan para migrar Default.aspx a Razor Pages"

---

**Nota**: Este ejemplo demuestra cómo el sistema ahora puede entender y analizar aplicaciones legacy WebForms, algo que antes era imposible sin procesamiento manual.
