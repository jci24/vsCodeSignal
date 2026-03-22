using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using VSCodeSignals.Api.Features.Workspaces.Common;
using VSCodeSignals.Api.Features.Workspaces.Handler;

namespace VSCodeSignals.Api.Features.Workspaces.Endpoint;

public sealed class GetWorkspaceImportFileContent(WorkspaceImportStore store) : EndpointWithoutRequest
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    public override void Configure()
    {
        Get("/current/imports/files/{fileId}/content");
        Group<WorkspaceGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var fileId = Route<string>("fileId");

        if (string.IsNullOrWhiteSpace(fileId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var file = store.GetImportedFile(fileId);

        if (file is null || !File.Exists(file.ResolvedPath))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        HttpContext.Response.ContentType = ResolveContentType(file.ResolvedPath, file.Format);
        HttpContext.Response.Headers.ContentDisposition =
            $"inline; filename*=UTF-8''{Uri.EscapeDataString(Path.GetFileName(file.SourcePath))}";

        await HttpContext.Response.SendFileAsync(file.ResolvedPath, ct);
    }

    private static string ResolveContentType(string path, string format)
    {
        if (ContentTypeProvider.TryGetContentType(path, out var contentType))
            return contentType;

        return format.ToLowerInvariant() switch
        {
            "aac" => "audio/aac",
            "aif" or "aiff" => "audio/aiff",
            "flac" => "audio/flac",
            "m4a" => "audio/mp4",
            "mp3" => "audio/mpeg",
            "ogg" => "audio/ogg",
            "wav" => "audio/wav",
            "wma" => "audio/x-ms-wma",
            _ => "application/octet-stream"
        };
    }
}
