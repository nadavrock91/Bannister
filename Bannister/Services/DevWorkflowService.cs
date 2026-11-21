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
        if (string.IsNullOrWhiteSpace(folder))
            return Unavailable("No repo folder set");
        if (!Directory.Exists(folder))
            return Unavailable($"Repo folder does not exist: {folder}");

        var git = FindGitExecutable();
        if (git.Path == null)
            return Unavailable($"git not found (looked in: {string.Join(", ", git.LookedIn)})");

        var repo = await RunGitAsync(git.Path, folder, "rev-parse", "--is-inside-work-tree");
        if (!repo.Started)
            return Unavailable($"git could not be started: {repo.Error}");
        if (repo.ExitCode != 0 || !repo.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
            return Unavailable("Folder is not a git repo");

        var fetch = await RunGitAsync(git.Path, folder, "fetch");
        if (!fetch.Started)
            return Unavailable($"git could not be started: {fetch.Error}");
        if (fetch.ExitCode != 0) return new() { IsAvailable = true, Message = fetch.Error.Trim(), Passed = false };
        var counts = await RunGitAsync(git.Path, folder, "rev-list", "--left-right", "--count", "HEAD...@{upstream}");
        if (!counts.Started)
            return Unavailable($"git could not be started: {counts.Error}");
        if (counts.ExitCode != 0)
        {
            string error = counts.Error.Trim();
            if (error.Contains("no upstream", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("unknown revision", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("ambiguous argument", StringComparison.OrdinalIgnoreCase))
                return Unavailable("No upstream branch");
            return Unavailable($"git failed: {NonEmpty(error, counts.Output)}");
        }
        var parts = counts.Output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int ahead = parts.Length > 0 && int.TryParse(parts[0], out var a) ? a : 0;
        int behind = parts.Length > 1 && int.TryParse(parts[1], out var b) ? b : 0;
        var status = await RunGitAsync(git.Path, folder, "status", "--porcelain");
        if (!status.Started)
            return Unavailable($"git could not be started: {status.Error}");
        if (status.ExitCode != 0)
            return Unavailable($"git failed: {NonEmpty(status.Error, status.Output)}");
        int changed = status.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        if (behind > 0) return new() { IsAvailable = true, IsBehind = true, Message = $"Behind by {behind} commits" };
        if (ahead > 0) return new() { IsAvailable = true, Message = $"{ahead} unpushed commits" };
        if (changed > 0) return new() { IsAvailable = true, Message = $"{changed} changed files" };
        return new() { IsAvailable = true, Passed = true, Message = "Up to date" };
#else
        await Task.CompletedTask;
        return Unavailable("Not available on this platform");
#endif
    }

    public async Task<DevGitCheckResult> PullAsync(string folder)
    {
#if WINDOWS
        if (string.IsNullOrWhiteSpace(folder)) return Unavailable("No repo folder set");
        if (!Directory.Exists(folder)) return Unavailable($"Repo folder does not exist: {folder}");
        var git = FindGitExecutable();
        if (git.Path == null)
            return Unavailable($"git not found (looked in: {string.Join(", ", git.LookedIn)})");
        var pull = await RunGitAsync(git.Path, folder, "pull", "--ff-only");
        if (!pull.Started) return Unavailable($"git could not be started: {pull.Error}");
        if (pull.ExitCode != 0)
            return new() { IsAvailable = true, Message = pull.Error.Trim(), Passed = false };
        return await CheckGitAsync(folder);
#else
        await Task.CompletedTask;
        return Unavailable("Not available on this platform");
#endif
    }

    private static DevGitCheckResult Unavailable(string message) => new()
    { IsAvailable = false, Passed = true, Message = message };

#if WINDOWS
    private static (string? Path, List<string> LookedIn) FindGitExecutable()
    {
        var candidates = new List<string>();
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try { candidates.Add(Path.Combine(directory.Trim().Trim('"'), "git.exe")); }
                catch { }
            }
        }
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "git.exe"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "cmd", "git.exe"));
        var distinct = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return (distinct.FirstOrDefault(File.Exists), distinct);
    }

    private static string NonEmpty(string first, string second) =>
        !string.IsNullOrWhiteSpace(first) ? first.Trim() :
        !string.IsNullOrWhiteSpace(second) ? second.Trim() : "Unknown error";

    private static async Task<(bool Started, int ExitCode, string Output, string Error)> RunGitAsync(
        string gitPath, string folder, params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = gitPath,
                WorkingDirectory = folder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            using var process = Process.Start(info);
            if (process == null) return (false, -1, "", "Process.Start returned no process.");
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (true, process.ExitCode, output, error);
        }
        catch (Exception ex) { return (false, -1, "", ex.Message); }
    }
#endif
}
