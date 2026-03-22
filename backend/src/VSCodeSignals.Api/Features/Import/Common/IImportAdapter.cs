namespace VSCodeSignals.Api.Features.Import.Common;

public interface IImportAdapter
{
    string Name { get; }
    bool CanImport(string path);
    Task<ImportedSignalFile> ImportAsync(string path, CancellationToken ct);
}
