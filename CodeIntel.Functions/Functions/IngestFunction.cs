using System.Net;
using System.Text.Json;
using CodeIntel.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace CodeIntel.Functions.Functions;

public sealed class IngestFunction
{
    private readonly IngestOrchestrator _orchestrator;

    public IngestFunction(IngestOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("ingest")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ingest")] HttpRequestData req,
        FunctionContext ctx)
    {
        var ct = ctx.CancellationToken;
        var body = await new StreamReader(req.Body).ReadToEndAsync(ct);

        RepoRequest? payload;
        try
        {
            payload = JsonSerializer.Deserialize<RepoRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            payload = null;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Owner) || string.IsNullOrWhiteSpace(payload.Repo))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid payload. Expected: { owner, repo, branch?, path? }", ct);
            return bad;
        }

        var result = await _orchestrator.RunAsync(payload, ct);
        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteAsJsonAsync(result, ct);
        return ok;
    }
}
