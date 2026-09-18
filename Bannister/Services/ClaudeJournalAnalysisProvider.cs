using System.Net.Http.Json;
using System.Text.Json;

namespace Bannister.Services;

public class ClaudeJournalAnalysisProvider : IJournalAnalysisProvider
{
    public string ProviderName => "Claude (in-app)";

    public async Task<string> AnalyzeAsync(string prompt, CancellationToken ct = default)
    {
        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync(
            "https://api.anthropic.com/v1/messages",
            new { model = "claude-sonnet-4-6", max_tokens = 4000,
                messages = new[] { new { role = "user", content = prompt } } }, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("content", out var content)) return "";
        foreach (var block in content.EnumerateArray())
            if (block.TryGetProperty("type", out var type) && type.GetString() == "text" &&
                block.TryGetProperty("text", out var text)) return text.GetString() ?? "";
        return "";
    }
}
