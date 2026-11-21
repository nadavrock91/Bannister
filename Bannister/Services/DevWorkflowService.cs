using System.Diagnostics;
using Bannister.Models;

namespace Bannister.Services;

public class DevWorkflowService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public DevWorkflowService(DatabaseService db) => _db = db;

    public async Task InitAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<DevWorkflow>();
        await conn.CreateTableAsync<DevWorkflowStep>();
        await conn.CreateTableAsync<DevProgrammingConstraint>();
        await conn.CreateTableAsync<DevWorkflowOverride>();
        await conn.CreateTableAsync<DevWorkflowDeviceSetting>();
        try { await conn.ExecuteAsync("ALTER TABLE dev_workflow_device_settings ADD COLUMN LastUsedWorkflowId INTEGER"); } catch { }
        _initialized = true;
    }

    public async Task<List<DevWorkflow>> GetWorkflowsAsync(string username)
    {
        await InitAsync(); var conn = await _db.GetConnectionAsync();
        return await conn.Table<DevWorkflow>().Where(x => x.Username == username)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync();
    }

    public async Task SaveWorkflowAsync(DevWorkflow item)
    {
        await InitAsync(); var conn = await _db.GetConnectionAsync();
        if (item.Id == 0) await conn.InsertAsync(item); else await conn.UpdateAsync(item);
    }

    public async Task DeleteWorkflowAsync(DevWorkflow item)
    {
        await InitAsync(); var conn = await _db.GetConnectionAsync();
        foreach (var step in await GetStepsAsync(item.Username, item.Id)) await conn.DeleteAsync(step);
        await conn.DeleteAsync(item);
    }

    public async Task<List<DevWorkflowStep>> GetStepsAsync(string username, int workflowId)
    {
        await InitAsync(); var conn = await _db.GetConnectionAsync();
        return await conn.Table<DevWorkflowStep>()
            .Where(x => x.Username == username && x.WorkflowId == workflowId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync();
    }

    public async Task SaveStepAsync(DevWorkflowStep item)
    {
        await InitAsync(); var conn = await _db.GetConnectionAsync();
        if (item.Id == 0) await conn.InsertAsync(item); else await conn.UpdateAsync(item);
    }

    public async Task DeleteStepAsync(DevWorkflowStep item)
    { await InitAsync(); var conn = await _db.GetConnectionAsync(); await conn.DeleteAsync(item); }

    public async Task<List<DevProgrammingConstraint>> GetConstraintsAsync(string username)
    {
        await InitAsync(); var conn = await _db.GetConnectionAsync();
        return await conn.Table<DevProgrammingConstraint>().Where(x => x.Username == username)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync();
    }

    public async Task SaveConstraintAsync(DevProgrammingConstraint item)
    {
        await InitAsync(); var conn = await _db.GetConnectionAsync();
        if (item.Id == 0) await conn.InsertAsync(item); else await conn.UpdateAsync(item);
    }

    public async Task DeleteConstraintAsync(DevProgrammingConstraint item)
    { await InitAsync(); var conn = await _db.GetConnectionAsync(); await conn.DeleteAsync(item); }

    public async Task SaveOverrideAsync(DevWorkflowOverride item)
    { await InitAsync(); var conn = await _db.GetConnectionAsync(); await conn.InsertAsync(item); }

    public async Task<List<DevWorkflowOverride>> GetOverridesAsync(string username)
    {
        await InitAsync(); var conn = await _db.GetConnectionAsync();
        return await conn.Table<DevWorkflowOverride>().Where(x => x.Username == username)
            .OrderByDescending(x => x.OverriddenAt).ToListAsync();
    }

    public async Task<string> GetRepoFolderAsync()
    {
        await InitAsync(); var conn = await _db.GetConnectionAsync();
        var row = await conn.Table<DevWorkflowDeviceSetting>()
            .Where(x => x.MachineName == Environment.MachineName).FirstOrDefaultAsync();
        return row?.RepoFolder ?? "";
    }

    public async Task SetRepoFolderAsync(string folder)
    {
        await InitAsync(); var conn = await _db.GetConnectionAsync();
        var row = await conn.Table<DevWorkflowDeviceSetting>()
            .Where(x => x.MachineName == Environment.MachineName).FirstOrDefaultAsync();
        if (row == null) await conn.InsertAsync(new DevWorkflowDeviceSetting
        { MachineName = Environment.MachineName, RepoFolder = folder });
        else { row.RepoFolder = folder; await conn.UpdateAsync(row); }
    }

    public async Task<int?> GetLastUsedWorkflowIdAsync()
    {
        await InitAsync(); var conn = await _db.GetConnectionAsync();
        var row = await conn.Table<DevWorkflowDeviceSetting>()
            .Where(x => x.MachineName == Environment.MachineName).FirstOrDefaultAsync();
        return row?.LastUsedWorkflowId;
    }

    public async Task SetLastUsedWorkflowIdAsync(int? workflowId)
    {
        await InitAsync(); var conn = await _db.GetConnectionAsync();
        var row = await conn.Table<DevWorkflowDeviceSetting>()
            .Where(x => x.MachineName == Environment.MachineName).FirstOrDefaultAsync();
        if (row == null)
            await conn.InsertAsync(new DevWorkflowDeviceSetting
            { MachineName = Environment.MachineName, LastUsedWorkflowId = workflowId });
        else { row.LastUsedWorkflowId = workflowId; await conn.UpdateAsync(row); }
    }

    public async Task SaveWorkflowWithStepsAsync(
        DevWorkflow workflow, IReadOnlyList<DevWorkflowStep> steps)
    {
        await SaveWorkflowAsync(workflow);
        var conn = await _db.GetConnectionAsync();
        var existing = await GetStepsAsync(workflow.Username, workflow.Id);
        var retainedIds = steps.Where(x => x.Id > 0).Select(x => x.Id).ToHashSet();
        foreach (var removed in existing.Where(x => !retainedIds.Contains(x.Id)))
            await conn.DeleteAsync(removed);
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            step.Username = workflow.Username;
            step.WorkflowId = workflow.Id;
            step.SortOrder = i;
            await SaveStepAsync(step);
        }
    }

    public async Task<DevGitCheckResult> CheckGitAsync(string folder)
    {
#if WINDOWS
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return Unavailable();
        var repo = await RunGitAsync(folder, "rev-parse", "--is-inside-work-tree");
        if (repo.ExitCode != 0 || !repo.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
            return Unavailable();
        var fetch = await RunGitAsync(folder, "fetch");
        if (fetch.ExitCode != 0) return new() { IsAvailable = true, Message = fetch.Error.Trim(), Passed = false };
        var counts = await RunGitAsync(folder, "rev-list", "--left-right", "--count", "HEAD...@{upstream}");
        if (counts.ExitCode != 0) return Unavailable();
        var parts = counts.Output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int ahead = parts.Length > 0 && int.TryParse(parts[0], out var a) ? a : 0;
        int behind = parts.Length > 1 && int.TryParse(parts[1], out var b) ? b : 0;
        var status = await RunGitAsync(folder, "status", "--porcelain");
        int changed = status.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        if (behind > 0) return new() { IsAvailable = true, IsBehind = true, Message = $"Behind by {behind} commits" };
        if (ahead > 0) return new() { IsAvailable = true, Message = $"{ahead} unpushed commits" };
        if (changed > 0) return new() { IsAvailable = true, Message = $"{changed} changed files" };
        return new() { IsAvailable = true, Passed = true, Message = "Up to date" };
#else
        await Task.CompletedTask;
        return Unavailable();
#endif
    }

    public async Task<DevGitCheckResult> PullAsync(string folder)
    {
#if WINDOWS
        var pull = await RunGitAsync(folder, "pull", "--ff-only");
        if (pull.ExitCode != 0)
            return new() { IsAvailable = true, Message = pull.Error.Trim(), Passed = false };
        return await CheckGitAsync(folder);
#else
        await Task.CompletedTask;
        return Unavailable();
#endif
    }

    private static DevGitCheckResult Unavailable() => new()
    { IsAvailable = false, Passed = true, Message = "Not available on this device" };

#if WINDOWS
    private static async Task<(int ExitCode, string Output, string Error)> RunGitAsync(
        string folder, params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = folder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            using var process = Process.Start(info);
            if (process == null) return (-1, "", "Git could not be started.");
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, output, error);
        }
        catch (Exception ex) { return (-1, "", ex.Message); }
    }
#endif
}
