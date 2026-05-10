using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AriadnaKnowledgeStore.Core;

namespace AriadnaKnowledgeStore.Ingest.Razor;

/// <summary>
/// Analyzes Razor (.cshtml) files from ASP.NET MVC/Core applications
/// </summary>
public sealed class RazorAnalyzer
{
    // Regex to detect @model directive
    private static readonly Regex ModelDirectiveRegex = new(
        @"@model\s+(?<type>[\w\.]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex to detect @inject directives
    private static readonly Regex InjectDirectiveRegex = new(
        @"@inject\s+(?<type>[\w\.]+)\s+(?<name>\w+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex to detect Component.InvokeAsync calls
    private static readonly Regex ComponentInvokeRegex = new(
        @"Component\.InvokeAsync\(\s*[""'](?<component>[\w]+)[""']\s*,?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex to detect Tag Helper controller/action attributes
    private static readonly Regex TagHelperControllerRegex = new(
        @"asp-controller\s*=\s*[""'](?<controller>[\w]+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TagHelperActionRegex = new(
        @"asp-action\s*=\s*[""'](?<action>[\w]+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex to detect Layout assignment
    private static readonly Regex LayoutRegex = new(
        @"Layout\s*=\s*[""'](?<layout>[^""']+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex to detect Html.Action/Url.Action calls
    private static readonly Regex HtmlActionRegex = new(
        @"@(?:Html|Url)\.Action\(\s*[""'](?<action>[\w]+)[""']\s*,\s*[""'](?<controller>[\w]+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<RazorAnalysisResult> AnalyzeAsync(string filePath, string rootPath, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        var relativePath = Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);

        // Extract @model directive
        var modelType = ExtractModelType(content);

        // Extract @inject directives
        var injectedServices = ExtractInjectedServices(content);

        // Extract Layout
        var layout = ExtractLayout(content);

        // Create Razor view node
        var viewId = $"razor:{relativePath}";
        var view = new RazorView(
            Id: viewId,
            Name: fileName,
            FilePath: relativePath,
            ModelType: modelType,
            Layout: layout,
            InjectedServices: injectedServices
        );

        // Extract ViewComponents
        var components = new List<ViewComponent>();
        var edges = new List<CodeEdge>();

        ExtractViewComponents(content, viewId, components, edges);

        // Extract Controller/Action references from Tag Helpers
        ExtractControllerActions(content, viewId, edges);

        // Create BindsToModel edge if model type exists
        if (!string.IsNullOrEmpty(modelType))
        {
            edges.Add(new CodeEdge(viewId, $"type:{modelType}", EdgeType.BindsToModel));
        }

        // Create UsesService edges for injected services
        foreach (var service in injectedServices)
        {
            edges.Add(new CodeEdge(viewId, $"service:{service}", EdgeType.UsesService));
        }

        // Create relationship to Layout if exists
        if (!string.IsNullOrEmpty(layout))
        {
            var layoutId = $"razor:{layout}";
            edges.Add(new CodeEdge(viewId, layoutId, EdgeType.DependsOn));
        }

        return new RazorAnalysisResult(view, components, edges);
    }

    private string? ExtractModelType(string content)
    {
        var match = ModelDirectiveRegex.Match(content);
        return match.Success ? match.Groups["type"].Value : null;
    }

    private List<string> ExtractInjectedServices(string content)
    {
        var services = new List<string>();
        var matches = InjectDirectiveRegex.Matches(content);

        foreach (Match match in matches)
        {
            services.Add(match.Groups["type"].Value);
        }

        return services;
    }

    private string? ExtractLayout(string content)
    {
        var match = LayoutRegex.Match(content);
        return match.Success ? match.Groups["layout"].Value : null;
    }

    private void ExtractViewComponents(string content, string viewId, List<ViewComponent> components, List<CodeEdge> edges)
    {
        var matches = ComponentInvokeRegex.Matches(content);
        var processedComponents = new HashSet<string>();

        foreach (Match match in matches)
        {
            var componentName = match.Groups["component"].Value;

            // Avoid duplicates
            if (processedComponents.Add(componentName))
            {
                var componentId = $"component:{componentName}";
                var component = new ViewComponent(
                    Id: componentId,
                    Name: componentName,
                    InvokedFrom: viewId
                );

                components.Add(component);
                edges.Add(new CodeEdge(viewId, componentId, EdgeType.UsesComponent));
            }
        }
    }

    private void ExtractControllerActions(string content, string viewId, List<CodeEdge> edges)
    {
        var processedActions = new HashSet<string>();

        // Extract from Tag Helpers (asp-controller/asp-action)
        var controllerMatches = TagHelperControllerRegex.Matches(content);
        var actionMatches = TagHelperActionRegex.Matches(content);

        // Simple pairing: assume controller and action are near each other
        for (int i = 0; i < Math.Min(controllerMatches.Count, actionMatches.Count); i++)
        {
            var controller = controllerMatches[i].Groups["controller"].Value;
            var action = actionMatches[i].Groups["action"].Value;
            var actionId = $"action:{controller}.{action}";

            if (processedActions.Add(actionId))
            {
                edges.Add(new CodeEdge(viewId, actionId, EdgeType.RenderedBy));
            }
        }

        // Extract from Html.Action/Url.Action calls
        var htmlActionMatches = HtmlActionRegex.Matches(content);
        foreach (Match match in htmlActionMatches)
        {
            var action = match.Groups["action"].Value;
            var controller = match.Groups["controller"].Value;
            var actionId = $"action:{controller}.{action}";

            if (processedActions.Add(actionId))
            {
                edges.Add(new CodeEdge(viewId, actionId, EdgeType.RenderedBy));
            }
        }
    }
}

/// <summary>
/// Result of analyzing a Razor view
/// </summary>
public record RazorAnalysisResult(
    RazorView View,
    List<ViewComponent> Components,
    List<CodeEdge> Edges
);
