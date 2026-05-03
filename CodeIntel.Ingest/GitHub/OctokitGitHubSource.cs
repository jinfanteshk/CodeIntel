using CodeIntel.Core;
using Octokit;

namespace CodeIntel.Ingest.GitHub;

/// <summary>
/// MVP: usa un token ya emitido (PAT u OAuth token). Para OAuth Web Flow, añade endpoints en Functions.
/// Descarga el contenido del repo vía API (recursivo) en un directorio temporal.
/// </summary>
public sealed class OctokitGitHubSource : IGitHubSource
{
    private readonly GitHubClient _client;

    public OctokitGitHubSource(string token)
    {
        _client = new GitHubClient(new ProductHeaderValue("codeintel-mvp"))
        {
            Credentials = new Credentials(token)
        };
    }

    public async Task<string> DownloadRepositoryAsync(RepoRequest req, CancellationToken ct)
    {
        // Directorio temporal
        var root = Path.Combine(Path.GetTempPath(), "codeintel", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        // Descarga recursiva empezando por path ("" => repo root)
        await DownloadPathAsync(req.Owner, req.Repo, req.Path ?? string.Empty, req.Branch, root, ct);
        return root;
    }

    private async Task DownloadPathAsync(string owner, string repo, string path, string branch, string localRoot, CancellationToken ct)
    {
        IReadOnlyList<RepositoryContent> items;
        try
        {
            items = string.IsNullOrWhiteSpace(path)
                ? await _client.Repository.Content.GetAllContentsByRef(owner, repo, branch)
                : await _client.Repository.Content.GetAllContentsByRef(owner, repo, path, branch);
        }
        catch (NotFoundException)
        {
            return;
        }

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            if (item.Type == ContentType.Dir)
            {
                var dir = Path.Combine(localRoot, item.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(dir);
                await DownloadPathAsync(owner, repo, item.Path, branch, localRoot, ct);
            }
            else if (item.Type == ContentType.File)
            {
                // Filtrado básico de binarios / grandes
                if (item.Size > 2_000_000) continue;

                var target = Path.Combine(localRoot, item.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                // Descarga raw
                var raw = await _client.Connection.Get<string>(new Uri(item.DownloadUrl), new Dictionary<string, string>(), "application/vnd.github.raw");
                await File.WriteAllTextAsync(target, raw.Body, ct);
            }
        }
    }
}
