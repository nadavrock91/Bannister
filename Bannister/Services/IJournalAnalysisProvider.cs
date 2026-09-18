namespace Bannister.Services;

public interface IJournalAnalysisProvider
{
    Task<string> AnalyzeAsync(string prompt, CancellationToken ct = default);
    string ProviderName { get; }
}
