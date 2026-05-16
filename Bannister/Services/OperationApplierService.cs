using System.Text.Json;
using Bannister.Models;

namespace Bannister.Services;

public record ApplyResult
{
    public string Uuid { get; init; } = "";
    public bool Success { get; init; }
    public bool Skipped { get; init; }
    public string Message { get; init; } = "";
}

public record ApplyBatchResult
{
    public int AppliedCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public List<string> UuidsToClear { get; init; } = new();
    public List<ApplyResult> Results { get; init; } = new();
}

public class OperationApplierService
{
    private readonly IdeasService _ideas;
    private readonly DatabaseService _db;
    private readonly AuthService _auth;

    public OperationApplierService(IdeasService ideas, DatabaseService db, AuthService auth)
    {
        _ideas = ideas;
        _db = db;
        _auth = auth;
    }

    public async Task<bool> IsAlreadyAppliedAsync(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid)) return false;

        var conn = await _db.GetConnectionAsync();
        var row = await conn.Table<AppliedOperation>()
            .Where(x => x.Uuid == uuid)
            .FirstOrDefaultAsync();

        return row != null;
    }

    public async Task RecordAppliedAsync(string uuid, string operationType, string? sourceDeviceId)
    {
        if (string.IsNullOrWhiteSpace(uuid)) return;
        if (await IsAlreadyAppliedAsync(uuid)) return;

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(new AppliedOperation
        {
            Uuid = uuid,
            OperationType = operationType,
            SourceDeviceId = sourceDeviceId,
            AppliedAt = DateTime.UtcNow
        });
    }

    public async Task<ApplyResult> ApplyOperationAsync(QueuedOperation op, string? sourceDeviceId)
    {
        if (string.IsNullOrWhiteSpace(op.Uuid))
            return new ApplyResult { Success = false, Uuid = "", Message = "Operation is missing a UUID." };

        try
        {
            if (await IsAlreadyAppliedAsync(op.Uuid))
                return new ApplyResult { Skipped = true, Uuid = op.Uuid, Message = "Already applied." };

            switch (op.OperationType)
            {
                case "idea_logged":
                    await ApplyIdeaLoggedAsync(op);
                    await RecordAppliedAsync(op.Uuid, op.OperationType, sourceDeviceId ?? op.SourceDeviceId);
                    return new ApplyResult { Success = true, Uuid = op.Uuid };

                default:
                    return new ApplyResult
                    {
                        Success = false,
                        Uuid = op.Uuid,
                        Message = $"Unknown operation type: {op.OperationType}"
                    };
            }
        }
        catch (Exception ex)
        {
            return new ApplyResult { Success = false, Uuid = op.Uuid, Message = ex.Message };
        }
    }

    public async Task<ApplyBatchResult> ApplyAllAsync(IEnumerable<QueuedOperation> ops, string? sourceDeviceIdLookup = null)
    {
        var results = new List<ApplyResult>();
        var uuidsToClear = new List<string>();
        int applied = 0;
        int skipped = 0;
        int failed = 0;

        foreach (var op in ops.OrderBy(o => o.CreatedAt))
        {
            var sourceDeviceId = op.SourceDeviceId ?? sourceDeviceIdLookup;
            var result = await ApplyOperationAsync(op, sourceDeviceId);
            results.Add(result);

            if (result.Success)
            {
                applied++;
                uuidsToClear.Add(result.Uuid);
            }
            else if (result.Skipped)
            {
                skipped++;
                uuidsToClear.Add(result.Uuid);
            }
            else
            {
                failed++;
            }
        }

        return new ApplyBatchResult
        {
            AppliedCount = applied,
            SkippedCount = skipped,
            FailedCount = failed,
            UuidsToClear = uuidsToClear,
            Results = results
        };
    }

    private async Task ApplyIdeaLoggedAsync(QueuedOperation op)
    {
        var password = _db.GetDbPassword();
        if (string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Not logged in; cannot decrypt queued operations.");

        string plaintext;
        try
        {
            plaintext = QueuePayloadCrypto.DecryptPayload(op.PayloadJson, password);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Could not decrypt queued operation - wrong password or corrupted payload.",
                ex);
        }

        using var doc = JsonDocument.Parse(plaintext);
        var root = doc.RootElement;

        var username = ReadString(root, "username");
        if (string.IsNullOrWhiteSpace(username))
            username = _auth.CurrentUsername;

        var title = ReadString(root, "title");
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Queued idea is missing a title.");

        var category = ReadString(root, "category");
        if (string.IsNullOrWhiteSpace(category))
            category = "General";

        var createdAt = ReadDateTime(root, "created_at") ?? op.CreatedAt;

        await _ideas.CreateIdeaAsync(
            username,
            title,
            category,
            notes: ReadString(root, "notes"),
            subcategory: ReadString(root, "subcategory"),
            rating: ReadInt(root, "rating", 50),
            isStarred: ReadBool(root, "is_starred", false),
            status: ReadInt(root, "status", 0),
            createdAt: createdAt);
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        return value.GetString();
    }

    private static int ReadInt(JsonElement root, string propertyName, int fallback)
    {
        if (!root.TryGetProperty(propertyName, out var value))
            return fallback;

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : fallback;
    }

    private static bool ReadBool(JsonElement root, string propertyName, bool fallback)
    {
        if (!root.TryGetProperty(propertyName, out var value))
            return fallback;

        return value.ValueKind == JsonValueKind.True || (value.ValueKind != JsonValueKind.False && fallback);
    }

    private static DateTime? ReadDateTime(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
            return null;

        return DateTime.TryParse(
            value.GetString(),
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
    }
}
