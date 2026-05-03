using System.Collections.Concurrent;
using CodeIntel.Core;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeIntel.Ingest.Roslyn;

/// <summary>
/// MVP: analiza archivos .cs sueltos.
/// Si quieres análisis cross-solution completo, abre .sln/.csproj con MSBuildWorkspace.
/// </summary>
public sealed class RoslynAnalyzer : ICodeAnalyzer
{
    public async Task<GraphModel> AnalyzeAsync(string localPath, CancellationToken ct)
    {
        var csFiles = Directory.EnumerateFiles(localPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .ToList();

        var classes = new ConcurrentBag<CodeClass>();
        var methods = new ConcurrentBag<CodeMethod>();
        var edges = new ConcurrentBag<CodeEdge>();

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

        return new GraphModel(classes.ToList(), methods.ToList(), edges.ToList());
    }

    private static string Rel(string file, string root)
        => Path.GetRelativePath(root, file).Replace('\\', '/');
}
