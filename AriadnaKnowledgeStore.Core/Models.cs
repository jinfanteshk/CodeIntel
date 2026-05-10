namespace AriadnaKnowledgeStore.Core;

public record RepoRequest(string Owner, string Repo, string Branch = "main", string Path = "");

public record CodeClass(string Id, string Name, string Namespace, string FilePath);
public record CodeMethod(string Id, string Name, string ClassId, string FilePath, string? Body);

// ASPX-specific models
public record AspxPage(string Id, string Name, string FilePath, string? CodeBehindClass, string? Inherits);
public record AspxControl(string Id, string Name, string Type, string PageId, string FilePath, Dictionary<string, string>? Events = null);
public record AspxEvent(string Id, string EventName, string ControlId, string HandlerMethod);

// Razor/MVC-specific models
public record RazorView(
    string Id,                      // "razor:/Views/Home/Index.cshtml"
    string Name,                    // "Index.cshtml"
    string FilePath,                // "Views/Home/Index.cshtml"
    string? ModelType,              // "Nop.Web.Models.ProductViewModel"
    string? Layout,                 // "_ColumnsOne"
    List<string> InjectedServices   // ["IWorkContext", "IUrlHelper"]
);

public record ViewComponent(
    string Id,                      // "component:Widget"
    string Name,                    // "Widget"
    string? InvokedFrom,            // "razor:/Views/Home/Index.cshtml"
    Dictionary<string, string>? Parameters = null
);

public record ControllerAction(
    string Id,                      // "action:Product.Details"
    string ControllerName,          // "Product"
    string ActionName,              // "Details"
    string? ReturnType              // "IActionResult"
);

public enum EdgeType
{
    DependsOn,
    Calls,
    Inherits,
    Implements,
    CodeBehind,      // ASPX page -> code-behind class
    HasControl,      // ASPX page -> Control
    HandlesEvent,    // Control -> Method handler
    // Razor/MVC-specific edges
    RenderedBy,      // Razor view -> Controller/Action
    UsesComponent,   // Razor view -> ViewComponent
    UsesService,     // Razor view -> Injected service
    BindsToModel,    // Razor view -> ViewModel/DTO
    ReturnsView      // Controller action -> Razor view
}

public record CodeEdge(string FromId, string ToId, EdgeType Type);

public record GraphModel(
    IReadOnlyList<CodeClass> Classes,
    IReadOnlyList<CodeMethod> Methods,
    IReadOnlyList<CodeEdge> Edges,
    IReadOnlyList<AspxPage> AspxPages,
    IReadOnlyList<AspxControl> AspxControls,
    IReadOnlyList<AspxEvent> AspxEvents,
    // Razor/MVC collections
    IReadOnlyList<RazorView> RazorViews,
    IReadOnlyList<ViewComponent> ViewComponents,
    IReadOnlyList<ControllerAction> ControllerActions);

public record VectorDocument(
    string Id,
    string Content,
    float[] Embedding,
    string Type,
    string ClassName,
    string FilePath);

public record VersionInfo(
    string VersionId, 
    string CommitHash, 
    DateTimeOffset Timestamp, 
    bool IsCurrent)
{
    public string? DatabaseName { get; init; }
}
