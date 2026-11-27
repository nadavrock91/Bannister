using Bannister.Models;
using SQLite;
using System.Text.Json;

namespace Bannister.Services;

public sealed record ThemePaletteResolution(Dictionary<string, string> Values, IReadOnlyList<string> MissingColors);
public sealed record ThemePaletteSaveResult(ActivityDisplayMode Mode, Dictionary<string, string> Values,
    int ChangedCount, IReadOnlyList<string> MissingColors);

public partial class AppSettingsService
{
    private static readonly ActivityDisplayMode[] PaletteModes =
        { ActivityDisplayMode.PrivateOnly, ActivityDisplayMode.PublicOnly, ActivityDisplayMode.All };

    private sealed record PaletteWrite(ActivityDisplayMode Mode, string Source, string? Note = null,
        IReadOnlyDictionary<string, string>? Values = null, string? Json = null, bool Reset = false);

    private static Dictionary<string, string> PaletteSnapshot(ActivityDisplayMode mode, IReadOnlyDictionary<string, string> settings)
    {
        var snapshot = ModeTheme.EditableHex(ModeTheme.Get(mode));
        string prefix = ModeTheme.StoragePrefix(mode);
        foreach (string name in snapshot.Keys.ToArray())
            if (settings.TryGetValue(prefix + name, out var value) && ModeTheme.TryNormalizeHex(value, out var hex))
                snapshot[name] = ModeTheme.ToHex(ModeTheme.ParseStoredHex(hex));
        return snapshot;
    }

    private static void EnsureStartingPoints(SQLiteConnection db, string username, IReadOnlyDictionary<string, string> settings)
    {
        foreach (var mode in PaletteModes)
        {
            string name = ModeTheme.PaletteModeName(mode);
            if (db.Table<ThemePaletteHistory>().Where(h => h.Username == username && h.Mode == name).Count() == 0)
                RecordPaletteHistory(db, username, mode, PaletteSnapshot(mode, settings), "Starting point");
        }
    }

    private static void RecordPaletteHistory(SQLiteConnection db, string username, ActivityDisplayMode mode,
        Dictionary<string, string> palette, string source, string? note = null)
    {
        DateTime now = DateTime.UtcNow;
        string modeName = ModeTheme.PaletteModeName(mode);
        var latest = db.Table<ThemePaletteHistory>().Where(h => h.Username == username).OrderByDescending(h => h.Id).FirstOrDefault();
        string json = JsonSerializer.Serialize(palette);
        if (source == "Manual edit" && latest?.Source == source && latest.Mode == modeName &&
            now >= latest.CreatedAt && now - latest.CreatedAt <= TimeSpan.FromSeconds(30))
        {
            latest.CreatedAt = now;
            latest.PaletteJson = json;
            latest.Note = note;
            db.Update(latest);
        }
        else db.Insert(new ThemePaletteHistory
        {
            Username = username, Mode = modeName, CreatedAt = now, Source = source, PaletteJson = json, Note = note
        });
        foreach (var old in db.Table<ThemePaletteHistory>().Where(h => h.Username == username && h.Mode == modeName)
                     .OrderByDescending(h => h.Id).Skip(100).ToList()) db.Delete(old);
    }

    private async Task<IReadOnlyList<ThemePaletteSaveResult>> WritePalettesAsync(string username, IEnumerable<PaletteWrite> writes)
    {
        await InitAsync();
        if (IsReadOnly) throw new InvalidOperationException("Colors can be changed on the master device.");
        await MigrateThemeOverridesAsync(username);
        var results = new List<ThemePaletteSaveResult>();
        await (await _db.GetConnectionAsync()).RunInTransactionAsync(db =>
        {
            var rows = db.Table<AppSetting>().Where(s => s.Username == username).OrderBy(s => s.Id).ToList();
            var settings = rows.GroupBy(s => s.Key).ToDictionary(g => g.Key, g => g.Last().Value);
            EnsureStartingPoints(db, username, settings);
            foreach (var write in writes)
            {
                if (!PaletteModes.Contains(write.Mode)) throw new InvalidOperationException("Unknown palette mode.");
                string prefix = ModeTheme.StoragePrefix(write.Mode);
                var before = PaletteSnapshot(write.Mode, settings);
                var after = write.Reset ? ModeTheme.EditableHex(ModeTheme.Get(write.Mode)) : new Dictionary<string, string>(before);
                IReadOnlyList<string> missing = Array.Empty<string>();
                if (write.Json != null)
                {
                    var resolved = ModeTheme.ResolvePalette(write.Json, before);
                    after = resolved.Values;
                    missing = resolved.MissingColors;
                }
                if (write.Values != null)
                    foreach (var (name, value) in write.Values)
                    {
                        if (!ModeTheme.EditableColors.Any(c => c.Name == name) || !ModeTheme.TryNormalizeHex(value, out string hex))
                            throw new InvalidOperationException($"Cannot save color '{name}'.");
                        after[name] = ModeTheme.ToHex(ModeTheme.ParseStoredHex(hex));
                    }
                int changed = after.Count(c => before[c.Key] != c.Value);
                if (changed > 0 || write.Source != "Manual edit")
                {
                    if (write.Reset)
                    {
                        foreach (var row in rows.Where(s => s.Key.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
                        { db.Delete(row); rows.Remove(row); settings.Remove(row.Key); }
                    }
                    else foreach (var (name, hex) in after)
                    {
                        // Keep a complete snapshot in history; write only requested/effectively changed overrides.
                        if (before[name] == hex && write.Json == null && write.Values?.ContainsKey(name) != true) continue;
                        string key = prefix + name;
                        var row = rows.LastOrDefault(s => s.Key == key);
                        if (row == null) { row = new AppSetting { Username = username, Key = key, Value = hex }; db.Insert(row); rows.Add(row); }
                        else { row.Value = hex; db.Update(row); }
                        settings[key] = hex;
                    }
                    RecordPaletteHistory(db, username, write.Mode, after, write.Source,
                        string.Join("\n", new[] { write.Note, missing.Count > 0 ? "Used current values for missing or invalid colors: " + string.Join(", ", missing) : null }
                            .Where(s => !string.IsNullOrWhiteSpace(s))));
                }
                results.Add(new(write.Mode, after, changed, missing));
            }
        });
        return results;
    }

    public async Task<ThemePaletteSaveResult> SaveThemePaletteAsync(string username, ActivityDisplayMode mode,
        IReadOnlyDictionary<string, string> values, string source = "Manual edit", string? note = null) =>
        (await WritePalettesAsync(username, new[] { new PaletteWrite(mode, source, note, Values: values) })).Single();

    public Task<IReadOnlyList<ThemePaletteSaveResult>> ResetThemePalettesAsync(string username, IEnumerable<ActivityDisplayMode> modes) =>
        WritePalettesAsync(username, modes.Distinct().Select(mode => new PaletteWrite(mode, "Reset", Reset: true)));

    private async Task<bool> PaletteTableExistsAsync(string table)
    {
        await InitAsync();
        return await (await _db.GetConnectionAsync()).ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?", table) > 0;
    }

    public async Task<IReadOnlyList<ThemePaletteHistory>> GetThemeHistoryAsync(string username, ActivityDisplayMode mode)
    {
        await MigrateThemeOverridesAsync(username);
        if (!await PaletteTableExistsAsync("theme_palette_history")) return Array.Empty<ThemePaletteHistory>();
        string name = ModeTheme.PaletteModeName(mode);
        return await (await _db.GetConnectionAsync()).Table<ThemePaletteHistory>()
            .Where(h => h.Username == username && h.Mode == name).OrderByDescending(h => h.Id).ToListAsync();
    }

    public async Task<ThemePaletteSaveResult> RestoreThemeHistoryAsync(string username, int id, string? expectedPaletteJson = null)
    {
        await InitAsync();
        var item = await (await _db.GetConnectionAsync()).Table<ThemePaletteHistory>()
            .Where(h => h.Username == username && h.Id == id).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("This history entry is no longer available.");
        if (expectedPaletteJson != null && item.PaletteJson != expectedPaletteJson)
            throw new InvalidOperationException("This history entry changed. Open it again to review the latest colors.");
        return (await WritePalettesAsync(username, new[] { new PaletteWrite(ModeTheme.ParsePaletteMode(item.Mode), "Restore",
            $"Restored entry {item.Id} from {item.CreatedAt.ToLocalTime():g} ({item.Source}).", Json: item.PaletteJson) })).Single();
    }

    private static List<ThemeTemplate> BuiltInTemplates(string username) => PaletteModes.Select((mode, index) => new ThemeTemplate
    {
        Id = -index - 1, Username = username, Name = "Bannister Default", Note = mode == ActivityDisplayMode.All ? "Both" : ModeTheme.PaletteModeName(mode),
        PaletteJson = JsonSerializer.Serialize(ModeTheme.EditableHex(ModeTheme.Get(mode)))
    }).ToList();

    public async Task<IReadOnlyList<ThemeTemplate>> GetThemeTemplatesAsync(string username)
    {
        var result = BuiltInTemplates(username);
        if (await PaletteTableExistsAsync("theme_templates"))
            result.AddRange(await (await _db.GetConnectionAsync()).Table<ThemeTemplate>()
                .Where(t => t.Username == username).OrderByDescending(t => t.UpdatedAt).ToListAsync());
        return result;
    }

    private static string TemplateName(string name)
    {
        name = name.Trim();
        if (name.Length == 0) throw new InvalidOperationException("Enter a template name.");
        if (string.Equals(name, "Bannister Default", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bannister Default is reserved for the built-in templates.");
        return name;
    }

    public async Task<ThemeTemplate> SaveThemeTemplateAsync(string username, string name, ActivityDisplayMode mode, bool overwrite = false, string? note = null)
    {
        name = TemplateName(name);
        await MigrateThemeOverridesAsync(username);
        if (IsReadOnly) throw new InvalidOperationException("Templates can be changed on the master device.");
        ThemeTemplate result = null!;
        await (await _db.GetConnectionAsync()).RunInTransactionAsync(db =>
        {
            var existing = db.Table<ThemeTemplate>().Where(t => t.Username == username).ToList()
                .FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null && !overwrite) throw new InvalidOperationException("A template with this name already exists.");
            var values = db.Table<AppSetting>().Where(s => s.Username == username).OrderBy(s => s.Id).ToList()
                .GroupBy(s => s.Key).ToDictionary(g => g.Key, g => g.Last().Value);
            result = existing ?? new ThemeTemplate { Username = username, CreatedAt = DateTime.UtcNow };
            result.Name = name; result.UpdatedAt = DateTime.UtcNow; result.Note = note ?? existing?.Note;
            result.PaletteJson = JsonSerializer.Serialize(PaletteSnapshot(mode, values));
            if (existing == null) db.Insert(result); else db.Update(result);
        });
        return result;
    }

    public async Task RenameThemeTemplateAsync(string username, int id, string name)
    {
        name = TemplateName(name);
        await InitAsync();
        if (IsReadOnly) throw new InvalidOperationException("Templates can be changed on the master device.");
        await (await _db.GetConnectionAsync()).RunInTransactionAsync(db =>
        {
            var own = db.Table<ThemeTemplate>().Where(t => t.Username == username).ToList();
            var item = own.FirstOrDefault(t => t.Id == id) ?? throw new InvalidOperationException("This template cannot be renamed.");
            if (own.Any(t => t.Id != id && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A template with this name already exists.");
            item.Name = name; item.UpdatedAt = DateTime.UtcNow; db.Update(item);
        });
    }

    public async Task DeleteThemeTemplateAsync(string username, int id)
    {
        await InitAsync();
        if (IsReadOnly) throw new InvalidOperationException("Templates can be changed on the master device.");
        await (await _db.GetConnectionAsync()).ExecuteAsync("DELETE FROM theme_templates WHERE Username = ? AND Id = ? AND Id > 0", username, id);
    }

    public async Task<IReadOnlyList<ThemePaletteSaveResult>> ApplyThemeTemplateAsync(string username, int id, IEnumerable<ActivityDisplayMode> modes,
        string? expectedPaletteJson = null)
    {
        var template = (await GetThemeTemplatesAsync(username)).FirstOrDefault(t => t.Id == id)
            ?? throw new InvalidOperationException("This template is no longer available.");
        if (expectedPaletteJson != null && template.PaletteJson != expectedPaletteJson)
            throw new InvalidOperationException("This template changed. Open it again to review the latest colors.");
        return await WritePalettesAsync(username, modes.Distinct().Select(mode => new PaletteWrite(mode,
            "Template applied: " + template.DisplayName, template.Note, Json: template.PaletteJson)));
    }
}
