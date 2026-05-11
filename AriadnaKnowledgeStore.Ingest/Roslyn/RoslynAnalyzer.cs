using System.Collections.Concurrent;
using AriadnaKnowledgeStore.Core;
using AriadnaKnowledgeStore.Ingest.Aspx;
using AriadnaKnowledgeStore.Ingest.Blazor;
using AriadnaKnowledgeStore.Ingest.Razor;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace AriadnaKnowledgeStore.Ingest.Roslyn;

/// <summary>
/// Analyzes C# files, ASPX/ASCX files, Razor (.cshtml) files, and Blazor (.razor) components from repositories
/// </summary>
public sealed class RoslynAnalyzer : ICodeAnalyzer
{
    private readonly AspxAnalyzer _aspxAnalyzer = new();
    private readonly RazorAnalyzer _razorAnalyzer = new();
    private readonly BlazorComponentAnalyzer _blazorAnalyzer = new();

    public async Task<GraphModel> AnalyzeAsync(string localPath, CancellationToken ct)
    {
        var csFiles = Directory.EnumerateFiles(localPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var aspxFiles = Directory.EnumerateFiles(localPath, "*.aspx", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(localPath, "*.ascx", SearchOption.AllDirectories))
            .ToList();

        var cshtmlFiles = Directory.EnumerateFiles(localPath, "*.cshtml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .ToList();

        var razorFiles = Directory.EnumerateFiles(localPath, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .ToList();

        var classes = new ConcurrentBag<CodeClass>();
        var methods = new ConcurrentBag<CodeMethod>();
        var edges = new ConcurrentBag<CodeEdge>();

        var aspxPages = new ConcurrentBag<AspxPage>();
        var aspxControls = new ConcurrentBag<AspxControl>();
        var aspxEvents = new ConcurrentBag<AspxEvent>();

        var razorViews = new ConcurrentBag<RazorView>();
        var viewComponents = new ConcurrentBag<ViewComponent>();
        var controllerActions = new ConcurrentBag<ControllerAction>();

        var blazorComponents = new ConcurrentBag<BlazorComponent>();
        var blazorParameters = new ConcurrentBag<BlazorParameter>();
        var blazorEventCallbacks = new ConcurrentBag<BlazorEventCallback>();
        var blazorChildComponents = new ConcurrentBag<BlazorChildComponent>();

        // Analyze C# files
        foreach (var file in csFiles)
        {
            ct.ThrowIfCancellationRequested();

            var text = await File.ReadAllTextAsync(file, ct);
            var tree = CSharpSyntaxTree.ParseText(text);
            var root = await tree.GetRootAsync(ct);

            var namespaceName = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "";

            foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var className = cls.Identifier.Text;
                var classId = $"class:{namespaceName}.{className}".TrimEnd('.');

                classes.Add(new CodeClass(classId, className, namespaceName, Rel(file, localPath)));

                // Herencia / interfaces (sintáctico simple)
                if (cls.BaseList is not null)
                {
                    foreach (var bt in cls.BaseList.Types)
                    {
                        var baseName = bt.Type.ToString();
                        var toId = $"type:{baseName}";
                        edges.Add(new CodeEdge(classId, toId, EdgeType.Inherits));
                    }
                }

                foreach (var m in cls.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    var methodName = m.Identifier.Text;
                    var methodId = $"method:{namespaceName}.{className}.{methodName}".TrimEnd('.');
                    var body = m.Body?.ToString();
                    methods.Add(new CodeMethod(methodId, methodName, classId, Rel(file, localPath), body));

                    // llamadas: muy básico (InvocationExpression)
                    foreach (var inv in m.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        var called = inv.Expression.ToString();
                        var toId2 = $"call:{called}";
                        edges.Add(new CodeEdge(methodId, toId2, EdgeType.Calls));
                    }

                    // dependencias por tipos usados (muy básico)
                    foreach (var id in m.DescendantNodes().OfType<IdentifierNameSyntax>())
                    {
                        var toId3 = $"token:{id.Identifier.Text}";
                        edges.Add(new CodeEdge(methodId, toId3, EdgeType.DependsOn));
                    }
                }
            }
        }

        // Analyze ASPX/ASCX files
        foreach (var aspxFile in aspxFiles)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var result = await _aspxAnalyzer.AnalyzeAsync(aspxFile, localPath, ct);

                aspxPages.Add(result.Page);

                foreach (var control in result.Controls)
                    aspxControls.Add(control);

                foreach (var evt in result.Events)
                    aspxEvents.Add(evt);

                foreach (var edge in result.Edges)
                    edges.Add(edge);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to analyze {aspxFile}: {ex.Message}");
            }
        }

        // Analyze Razor (.cshtml) files
        foreach (var cshtmlFile in cshtmlFiles)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var result = await _razorAnalyzer.AnalyzeAsync(cshtmlFile, localPath, ct);

                razorViews.Add(result.View);

                foreach (var component in result.Components)
                    viewComponents.Add(component);

                foreach (var edge in result.Edges)
                    edges.Add(edge);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to analyze Razor file {cshtmlFile}: {ex.Message}");
            }
        }

        // Analyze Blazor (.razor) components
        foreach (var razorFile in razorFiles)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var result = await _blazorAnalyzer.AnalyzeAsync(razorFile, localPath, ct);

                blazorComponents.Add(result.Component);

                foreach (var parameter in result.Parameters)
                    blazorParameters.Add(parameter);

                foreach (var callback in result.EventCallbacks)
                    blazorEventCallbacks.Add(callback);

                foreach (var child in result.ChildComponents)
                    blazorChildComponents.Add(child);

                foreach (var edge in result.Edges)
                    edges.Add(edge);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to analyze Blazor component {razorFile}: {ex.Message}");
            }
        }

        return new GraphModel(
            classes.ToList(), 
            methods.ToList(), 
            edges.ToList(),
            aspxPages.ToList(),
            aspxControls.ToList(),
            aspxEvents.ToList(),
            // Razor collections
            razorViews.ToList(),
            viewComponents.ToList(),
            controllerActions.ToList(),
            // Blazor collections
            blazorComponents.ToList(),
            blazorParameters.ToList(),
            blazorEventCallbacks.ToList(),
            blazorChildComponents.ToList());
    }

    private static string Rel(string file, string root)
        => Path.GetRelativePath(root, file).Replace('\\', '/');
}
