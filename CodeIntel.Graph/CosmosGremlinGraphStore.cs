using CodeIntel.Core;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using System.Collections.Generic;       
using System.Threading;
using System.Threading.Tasks;

namespace CodeIntel.Graph;

public sealed class CosmosGremlinGraphStore : IGraphStore
{
    private readonly GremlinClient _client;

    public CosmosGremlinGraphStore(string host, int port, string database, string graph, string key)
    {
        // username según Cosmos Gremlin
        var username = $"/dbs/{database}/colls/{graph}";

        var server = new GremlinServer(host, port, enableSsl: true, username: username, password: key);
        // El SDK puede no exponer la constante GraphSON2MimeType; usar el MIME type correspondiente directamente.
        _client = new GremlinClient(server, new GraphSON2Reader(), new GraphSON2Writer(), "application/vnd.gremlin-v2.0+json");
    }

    public async Task UpsertAsync(RepoRequest req, GraphModel model, CancellationToken ct)
    {
        // MVP: upsert simple (no hace limpieza). En producción: versionado por ingestId y limpieza por repo.
        // Añadimos vertices de clases y métodos, y edges.

        foreach (var c in model.Classes)
        {
            ct.ThrowIfCancellationRequested();
            var q = "g.V(id).fold().coalesce(unfold(), addV('Class').property('id', id)).property('name', name).property('ns', ns).property('file', file).property('repo', repo)";
            var bindings = new Dictionary<string, object>
            {
                ["id"] = c.Id,
                ["name"] = c.Name,
                ["ns"] = c.Namespace,
                ["file"] = c.FilePath,
                ["repo"] = $"{req.Owner}/{req.Repo}@{req.Branch}"
            };
            await _client.SubmitAsync<dynamic>(q, bindings);
        }

        foreach (var m in model.Methods)
        {
            ct.ThrowIfCancellationRequested();
            var q = "g.V(id).fold().coalesce(unfold(), addV('Method').property('id', id)).property('name', name).property('file', file).property('classId', classId).property('repo', repo)";
            var bindings = new Dictionary<string, object>
            {
                ["id"] = m.Id,
                ["name"] = m.Name,
                ["file"] = m.FilePath,
                ["classId"] = m.ClassId,
                ["repo"] = $"{req.Owner}/{req.Repo}@{req.Branch}"
            };
            await _client.SubmitAsync<dynamic>(q, bindings);

            // relacion método -> clase
            var q2 = "g.V(mid).as('m').V(cid).coalesce(__.inE('declared_in').where(outV().hasId(mid)), __.addE('declared_in').from('m'))";
            var b2 = new Dictionary<string, object> { ["mid"] = m.Id, ["cid"] = m.ClassId };
            await _client.SubmitAsync<dynamic>(q2, b2);
        }

        foreach (var e in model.Edges)
        {
            ct.ThrowIfCancellationRequested();
            var label = e.Type.ToString().ToLowerInvariant();
            var q = "g.V(from).as('a').V(to).coalesce(__.inE(lbl).where(outV().hasId(from)), __.addE(lbl).from('a'))";
            var bindings = new Dictionary<string, object>
            {
                ["from"] = e.FromId,
                ["to"] = e.ToId,
                ["lbl"] = label
            };
            await _client.SubmitAsync<dynamic>(q, bindings);
        }
    }
}
