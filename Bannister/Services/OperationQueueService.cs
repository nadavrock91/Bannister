using System.Text.Json;
using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class OperationQueueService
{
    private readonly DatabaseService _db;
    private readonly string _dbPath = Path.Combine(FileSystem.AppDataDirectory, "bannister_queue.db");
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private SQLiteAsyncConnection? _conn;
    private bool _initialized;

    public OperationQueueService(DatabaseService db)
    {
        _db = db;
    }

    public async Task InitAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            _conn = new SQLiteAsyncConnection(
                _dbPath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex,
                storeDateTimeAsTicks: false);

            await _conn.CreateTableAsync<QueuedOperation>();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<string> EnqueueAsync(string operationType, object payload)
    {
        await InitAsync();

        var password = _db.GetDbPassword();
        if (string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Cannot queue operation because no user is logged in.");

        var plaintext = JsonSerializer.Serialize(payload);
        var encryptedPayload = QueuePayloadCrypto.EncryptPayload(plaintext, password);
        var uuid = Guid.NewGuid().ToString("D");
        var operation = new QueuedOperation
        {
            Uuid = uuid,
            OperationType = operationType,
            PayloadJson = encryptedPayload,
            CreatedAt = DateTime.UtcNow,
            Status = 0
        };

        await _conn!.InsertAsync(operation);
        return uuid;
    }

    public async Task<List<QueuedOperation>> GetPendingAsync()
    {
        await InitAsync();
        return await _conn!.Table<QueuedOperation>()
            .Where(o => o.Status == 0)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetPendingCountAsync()
    {
        await InitAsync();
        return await _conn!.Table<QueuedOperation>()
            .Where(o => o.Status == 0)
            .CountAsync();
    }

    public async Task MarkSyncedAsync(string uuid)
    {
        await UpdateStatusAsync(uuid, 1, syncedAt: DateTime.UtcNow);
    }

    public async Task MarkAppliedAsync(string uuid)
    {
        await UpdateStatusAsync(uuid, 2, appliedAt: DateTime.UtcNow);
    }

    public async Task MarkFailedAsync(string uuid, string? failureReason)
    {
        await UpdateStatusAsync(uuid, 3, failureReason: failureReason);
    }

    private async Task UpdateStatusAsync(
        string uuid,
        int status,
        DateTime? syncedAt = null,
        DateTime? appliedAt = null,
        string? failureReason = null)
    {
        await InitAsync();

        var operation = await _conn!.Table<QueuedOperation>()
            .Where(o => o.Uuid == uuid)
            .FirstOrDefaultAsync();

        if (operation == null) return;

        operation.Status = status;
        if (syncedAt.HasValue) operation.SyncedAt = syncedAt;
        if (appliedAt.HasValue) operation.AppliedAt = appliedAt;
        operation.FailureReason = failureReason;

        await _conn.UpdateAsync(operation);
    }
}
