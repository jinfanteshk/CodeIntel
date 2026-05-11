using System.Text.RegularExpressions;
using AriadnaKnowledgeStore.Core;

namespace AriadnaKnowledgeStore.Ingest.Blazor;

/// <summary>
/// Analyzes Blazor Component (.razor) files
/// </summary>
public sealed class BlazorComponentAnalyzer
{
    // Regex to detect @page directive
    private static readonly Regex PageDirectiveRegex = new(
        @"@page\s+[""'](?<route>[^""']+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex to detect @inherits directive
    private static readonly Regex InheritsDirectiveRegex = new(
        @"@inherits\s+(?<type>[\w\.<>]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex to detect @inject directives
    private static readonly Regex InjectDirectiveRegex = new(
        @"@inject\s+(?<type>[\w\.]+)\s+(?<name>\w+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex to detect @layout directive
    private static readonly Regex LayoutDirectiveRegex = new(
        @"@layout\s+(?<layout>[\w]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex to extract @code block
    private static readonly Regex CodeBlockRegex = new(
        @"@code\s*\{(?<code>.*?)\n\}",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    // Regex to detect [Parameter] properties
    private static readonly Regex ParameterRegex = new(
        @"\[Parameter(?:\(.*?\))?\]\s*(?:public|internal|private)?\s*(?<type>[\w\.<>]+)\s+(?<name>\w+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex to detect [EditorRequired] attribute
    private static readonly Regex EditorRequiredRegex = new(
        @"\[EditorRequired\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex to detect EventCallback properties
    private static readonly Regex EventCallbackRegex = new(
        @"\[Parameter\]\s*public\s+EventCallback(?:<(?<type>[\w\.]+)>)?\s+(?<name>\w+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex to detect child component usage (simplified)
    private static readonly Regex ChildComponentRegex = new(
        @"<(?<component>[A-Z][\w]+)(?:\s|>|/>)",
        RegexOptions.Compiled);

    public async Task<BlazorAnalysisResult> AnalyzeAsync(string filePath, string rootPath, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        var relativePath = Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);
        var componentName = Path.GetFileNameWithoutExtension(fileName);

        // Extract directives
        var pageRoute = ExtractPageRoute(content);
        var baseType = ExtractInherits(content);
        var injectedServices = ExtractInjectedServices(content);
        var layout = ExtractLayout(content);
        var codeBlock = ExtractCodeBlock(content);

        // Create component node
        var componentId = $"blazor:{relativePath}";
        var component = new BlazorComponent(
            Id: componentId,
            Name: componentName,
            FilePath: relativePath,
            PageRoute: pageRoute,
            BaseType: baseType,
            InjectedServices: injectedServices,
            CodeBlock: codeBlock
        );

        // Extract parameters, callbacks, child components
        var parameters = new List<BlazorParameter>();
        var eventCallbacks = new List<BlazorEventCallback>();
        var childComponents = new List<BlazorChildComponent>();
        var edges = new List<CodeEdge>();

        ExtractParameters(content, componentId, parameters, edges);
        ExtractEventCallbacks(content, componentId, eventCallbacks, edges);
        ExtractChildComponents(content, componentId, childComponents, edges);

        // Create edges for services
        foreach (var service in injectedServices)
        {
            edges.Add(new CodeEdge(componentId, $"service:{service}", EdgeType.UsesService));
        }

        // Create edge for base type
        if (!string.IsNullOrEmpty(baseType) && baseType != "ComponentBase")
        {
            edges.Add(new CodeEdge(componentId, $"type:{baseType}", EdgeType.InheritsComponent));
        }

        // Create edge for page route
        if (!string.IsNullOrEmpty(pageRoute))
        {
            edges.Add(new CodeEdge($"route:{pageRoute}", componentId, EdgeType.RoutesToComponent));
        }

        // Create edge for layout
        if (!string.IsNullOrEmpty(layout))
        {
            edges.Add(new CodeEdge(componentId, $"blazor:/Shared/{layout}.razor", EdgeType.DependsOn));
        }

        return new BlazorAnalysisResult(component, parameters, eventCallbacks, childComponents, edges);
    }

    private string? ExtractPageRoute(string content)
    {
        var match = PageDirectiveRegex.Match(content);
        return match.Success ? match.Groups["route"].Value : null;
    }

    private string? ExtractInherits(string content)
    {
        var match = InheritsDirectiveRegex.Match(content);
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
        var match = LayoutDirectiveRegex.Match(content);
        return match.Success ? match.Groups["layout"].Value : null;
    }

    private string? ExtractCodeBlock(string content)
    {
        var match = CodeBlockRegex.Match(content);
        return match.Success ? match.Groups["code"].Value.Trim() : null;
    }

    private void ExtractParameters(string content, string componentId, List<BlazorParameter> parameters, List<CodeEdge> edges)
    {
        var codeBlock = ExtractCodeBlock(content) ?? "";
        var matches = ParameterRegex.Matches(codeBlock);

        foreach (Match match in matches)
        {
            var paramType = match.Groups["type"].Value;
            var paramName = match.Groups["name"].Value;

            // Check if parameter is required by looking backwards for [EditorRequired]
            var paramIndex = match.Index;
            var precedingText = codeBlock.Substring(Math.Max(0, paramIndex - 100), Math.Min(100, paramIndex));
            var isRequired = EditorRequiredRegex.IsMatch(precedingText);

            var paramId = $"param:{componentId}#{paramName}";
            var parameter = new BlazorParameter(
                Id: paramId,
                ComponentId: componentId,
                Name: paramName,
                Type: paramType,
                IsRequired: isRequired
            );

            parameters.Add(parameter);
            edges.Add(new CodeEdge(componentId, paramId, EdgeType.HasParameter));
        }
    }

    private void ExtractEventCallbacks(string content, string componentId, List<BlazorEventCallback> eventCallbacks, List<CodeEdge> edges)
    {
        var codeBlock = ExtractCodeBlock(content) ?? "";
        var matches = EventCallbackRegex.Matches(codeBlock);

        foreach (Match match in matches)
        {
            var callbackName = match.Groups["name"].Value;
            var eventType = match.Groups["type"].Success ? match.Groups["type"].Value : null;

            var callbackId = $"callback:{componentId}#{callbackName}";
            var callback = new BlazorEventCallback(
                Id: callbackId,
                ComponentId: componentId,
                Name: callbackName,
                EventType: eventType
            );

            eventCallbacks.Add(callback);
            edges.Add(new CodeEdge(componentId, callbackId, EdgeType.HasEventCallback));
        }
    }

    private void ExtractChildComponents(string content, string componentId, List<BlazorChildComponent> childComponents, List<CodeEdge> edges)
    {
        var matches = ChildComponentRegex.Matches(content);
        var processedComponents = new HashSet<string>();

        foreach (Match match in matches)
        {
            var childName = match.Groups["component"].Value;

            // Filter out HTML tags and common Blazor built-ins
            if (IsHtmlTag(childName) || IsBuiltInComponent(childName))
                continue;

            // Avoid duplicates
            if (processedComponents.Add(childName))
            {
                var childId = $"child:{componentId}>{childName}";
                var child = new BlazorChildComponent(
                    Id: childId,
                    ParentComponentId: componentId,
                    ChildComponentName: childName
                );

                childComponents.Add(child);
                edges.Add(new CodeEdge(componentId, $"blazor:*/{childName}.razor", EdgeType.UsesChildComponent));
            }
        }
    }

    private static bool IsHtmlTag(string name)
    {
        var htmlTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Div", "Span", "Form", "Input", "Button", "Table", "Thead", "Tbody", "Tr", "Td", "Th",
            "Ul", "Ol", "Li", "Nav", "Header", "Footer", "Section", "Article", "Aside", "Main",
            "Label", "Select", "Option", "Textarea", "A", "P", "H1", "H2", "H3", "H4", "H5", "H6"
        };
        return htmlTags.Contains(name);
    }

    private static bool IsBuiltInComponent(string name)
    {
        var builtIns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CascadingValue", "CascadingParameter", "LayoutView", "RouteView", "Router",
            "NavLink", "NavigationManager", "ErrorBoundary", "Virtualize", "DynamicComponent",
            "HeadContent", "PageTitle", "FocusOnNavigate", "InputText", "InputNumber",
            "InputDate", "InputCheckbox", "InputSelect", "EditForm", "ValidationSummary",
            "ValidationMessage", "DataAnnotationsValidator"
        };
        return builtIns.Contains(name);
    }
}

/// <summary>
/// Result of analyzing a Blazor component
/// </summary>
public record BlazorAnalysisResult(
    BlazorComponent Component,
    List<BlazorParameter> Parameters,
    List<BlazorEventCallback> EventCallbacks,
    List<BlazorChildComponent> ChildComponents,
    List<CodeEdge> Edges
);
