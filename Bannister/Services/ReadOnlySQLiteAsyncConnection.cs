using System.Collections;
using System.Linq.Expressions;
using SQLite;

namespace Bannister.Services;

/// <summary>
/// Managed guard around sqlite-net's async connection. In secondary mode the
/// underlying SQLite connection is still opened read-only, but this wrapper
/// catches normal sqlite-net write APIs before they cross into SQLite/native
/// code and before Android can terminate the process from an uncaught async
/// void exception.
/// </summary>
public sealed class ReadOnlySQLiteAsyncConnection : ISQLiteAsyncConnection
{
    private readonly SQLiteAsyncConnection _inner;

    public ReadOnlySQLiteAsyncConnection(SQLiteAsyncConnection inner)
    {
        _inner = inner;
    }

    public string DatabasePath => _inner.DatabasePath;
    public bool StoreDateTimeAsTicks => _inner.StoreDateTimeAsTicks;
    public bool StoreTimeSpanAsTicks => _inner.StoreTimeSpanAsTicks;
    public string DateTimeStringFormat => _inner.DateTimeStringFormat;
    public int LibVersionNumber => _inner.LibVersionNumber;
    public IEnumerable<TableMapping> TableMappings => _inner.TableMappings;
    public bool TimeExecution { get => _inner.TimeExecution; set => _inner.TimeExecution = value; }
    public bool Trace { get => _inner.Trace; set => _inner.Trace = value; }
    public Action<string> Tracer { get => _inner.Tracer; set => _inner.Tracer = value; }

    public Task BackupAsync(string destinationDatabasePath, string databaseName = "main") =>
        _inner.BackupAsync(destinationDatabasePath, databaseName);

    public Task CloseAsync() => _inner.CloseAsync();

    public Task<int> CreateIndexAsync(string indexName, string tableName, string columnName, bool unique = false) =>
        BlockIntAsync();

    public Task<int> CreateIndexAsync(string indexName, string tableName, string[] columnNames, bool unique = false) =>
        BlockIntAsync();

    public Task<int> CreateIndexAsync(string tableName, string columnName, bool unique = false) =>
        BlockIntAsync();

    public Task<int> CreateIndexAsync(string tableName, string[] columnNames, bool unique = false) =>
        BlockIntAsync();

    public Task<int> CreateIndexAsync<T>(Expression<Func<T, object>> property, bool unique = false) =>
        BlockIntAsync();

    public Task<CreateTableResult> CreateTableAsync<T>(CreateFlags createFlags = CreateFlags.None) where T : new() =>
        BlockCreateTableAsync();

    public Task<CreateTableResult> CreateTableAsync(Type ty, CreateFlags createFlags = CreateFlags.None) =>
        BlockCreateTableAsync();

    public Task<CreateTablesResult> CreateTablesAsync(CreateFlags createFlags = CreateFlags.None, params Type[] types) =>
        BlockCreateTablesAsync();

    public Task<CreateTablesResult> CreateTablesAsync<T, T2>(CreateFlags createFlags = CreateFlags.None)
        where T : new()
        where T2 : new() =>
        BlockCreateTablesAsync();

    public Task<CreateTablesResult> CreateTablesAsync<T, T2, T3>(CreateFlags createFlags = CreateFlags.None)
        where T : new()
        where T2 : new()
        where T3 : new() =>
        BlockCreateTablesAsync();

    public Task<CreateTablesResult> CreateTablesAsync<T, T2, T3, T4>(CreateFlags createFlags = CreateFlags.None)
        where T : new()
        where T2 : new()
        where T3 : new()
        where T4 : new() =>
        BlockCreateTablesAsync();

    public Task<CreateTablesResult> CreateTablesAsync<T, T2, T3, T4, T5>(CreateFlags createFlags = CreateFlags.None)
        where T : new()
        where T2 : new()
        where T3 : new()
        where T4 : new()
        where T5 : new() =>
        BlockCreateTablesAsync();

    public Task<IEnumerable<object>> DeferredQueryAsync(TableMapping map, string query, params object[] args) =>
        _inner.DeferredQueryAsync(map, query, args);

    public Task<IEnumerable<T>> DeferredQueryAsync<T>(string query, params object[] args) where T : new() =>
        _inner.DeferredQueryAsync<T>(query, args);

    public Task<int> DeleteAllAsync<T>() => BlockIntAsync();

    public Task<int> DeleteAllAsync(TableMapping map) => BlockIntAsync();

    public Task<int> DeleteAsync(object objectToDelete) => BlockIntAsync();

    public Task<int> DeleteAsync<T>(object primaryKey) => BlockIntAsync();

    public Task<int> DeleteAsync(object primaryKey, TableMapping map) => BlockIntAsync();

    public Task<int> DropTableAsync<T>() where T : new() => BlockIntAsync();

    public Task<int> DropTableAsync(TableMapping map) => BlockIntAsync();

    public Task EnableLoadExtensionAsync(bool enabled) => _inner.EnableLoadExtensionAsync(enabled);

    public Task EnableWriteAheadLoggingAsync() => BlockTaskAsync();

    public Task<int> ExecuteAsync(string query, params object[] args) => BlockIntAsync();

    public Task<T> ExecuteScalarAsync<T>(string query, params object[] args) =>
        _inner.ExecuteScalarAsync<T>(query, args);

    public Task<T> FindAsync<T>(object pk) where T : new() =>
        _inner.FindAsync<T>(pk);

    public Task<T> FindAsync<T>(Expression<Func<T, bool>> predicate) where T : new() =>
        _inner.FindAsync(predicate);

    public Task<object> FindAsync(object pk, TableMapping map) =>
        _inner.FindAsync(pk, map);

    public Task<T> FindWithQueryAsync<T>(string query, params object[] args) where T : new() =>
        _inner.FindWithQueryAsync<T>(query, args);

    public Task<object> FindWithQueryAsync(TableMapping map, string query, params object[] args) =>
        _inner.FindWithQueryAsync(map, query, args);

    public Task<T> GetAsync<T>(object pk) where T : new() =>
        _inner.GetAsync<T>(pk);

    public Task<T> GetAsync<T>(Expression<Func<T, bool>> predicate) where T : new() =>
        _inner.GetAsync(predicate);

    public Task<object> GetAsync(object pk, TableMapping map) =>
        _inner.GetAsync(pk, map);

    public TimeSpan GetBusyTimeout() => _inner.GetBusyTimeout();

    public SQLiteConnectionWithLock GetConnection() => _inner.GetConnection();

    public Task<TableMapping> GetMappingAsync(Type type, CreateFlags createFlags = CreateFlags.None) =>
        _inner.GetMappingAsync(type, createFlags);

    public Task<TableMapping> GetMappingAsync<T>(CreateFlags createFlags = CreateFlags.None) where T : new() =>
        _inner.GetMappingAsync<T>(createFlags);

    public Task<List<SQLiteConnection.ColumnInfo>> GetTableInfoAsync(string tableName) =>
        _inner.GetTableInfoAsync(tableName);

    public Task<int> InsertAllAsync(IEnumerable objects, bool runInTransaction = true) => BlockIntAsync();

    public Task<int> InsertAllAsync(IEnumerable objects, string extra, bool runInTransaction = true) => BlockIntAsync();

    public Task<int> InsertAllAsync(IEnumerable objects, Type objType, bool runInTransaction = true) => BlockIntAsync();

    public Task<int> InsertAsync(object obj) => BlockIntAsync();

    public Task<int> InsertAsync(object obj, string extra) => BlockIntAsync();

    public Task<int> InsertAsync(object obj, Type objType) => BlockIntAsync();

    public Task<int> InsertAsync(object obj, string extra, Type objType) => BlockIntAsync();

    public Task<int> InsertOrReplaceAsync(object obj) => BlockIntAsync();

    public Task<int> InsertOrReplaceAsync(object obj, Type objType) => BlockIntAsync();

    public Task<List<object>> QueryAsync(TableMapping map, string query, params object[] args) =>
        _inner.QueryAsync(map, query, args);

    public Task<List<T>> QueryAsync<T>(string query, params object[] args) where T : new() =>
        _inner.QueryAsync<T>(query, args);

    public Task<List<T>> QueryScalarsAsync<T>(string query, params object[] args) =>
        _inner.QueryScalarsAsync<T>(query, args);

    public Task ReKeyAsync(string key) => BlockTaskAsync();

    public Task ReKeyAsync(byte[] key) => BlockTaskAsync();

    public Task RunInTransactionAsync(Action<SQLiteConnection> action) => BlockTaskAsync();

    public Task SetBusyTimeoutAsync(TimeSpan value) => _inner.SetBusyTimeoutAsync(value);

    public AsyncTableQuery<T> Table<T>() where T : new() => _inner.Table<T>();

    public Task<int> UpdateAllAsync(IEnumerable objects, bool runInTransaction = true) => BlockIntAsync();

    public Task<int> UpdateAsync(object obj) => BlockIntAsync();

    public Task<int> UpdateAsync(object obj, Type objType) => BlockIntAsync();

    private static Task<int> BlockIntAsync()
    {
        ReadOnlyModeNotifier.ShowBlockedWriteMessage();
        return Task.FromResult(0);
    }

    private static Task BlockTaskAsync()
    {
        ReadOnlyModeNotifier.ShowBlockedWriteMessage();
        return Task.CompletedTask;
    }

    private static Task<CreateTableResult> BlockCreateTableAsync()
    {
        ReadOnlyModeNotifier.ShowBlockedWriteMessage();
        return Task.FromResult(CreateTableResult.Migrated);
    }

    private static Task<CreateTablesResult> BlockCreateTablesAsync()
    {
        ReadOnlyModeNotifier.ShowBlockedWriteMessage();
        return Task.FromResult(new CreateTablesResult());
    }
}
