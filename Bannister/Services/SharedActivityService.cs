using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bannister.Models;

namespace Bannister.Services;

public class SharedActivityService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public SharedActivityService(DatabaseService db) => _db = db;

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await _db.EnsureTableAsync<SharedActivityLink>();
    }

    public async Task<List<SharedActivityLink>> GetLinksAsync(string username)
    {
        await EnsureInitializedAsync();
        try
        {
            var conn = await _db.GetConnectionAsync();
            return (await conn.Table<SharedActivityLink>()
                .Where(l => l.Username == username && l.IsActive)
                .ToListAsync())
                .OrderBy(l => l.PartnerName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch { return new(); }
    }

    public async Task<SharedActivityLink> CreateLinkAsync(
        string username, string linkCode, string partnerName,
        string password,
        List<(string GameId, string ActivityName)> activities)
    {
        await EnsureInitializedAsync();
        var link = new SharedActivityLink
        {
            Username = username,
            LinkCode = linkCode.Trim().ToUpperInvariant(),
            PartnerName = partnerName.Trim(),
            PasswordHash = HashPassword(password),
            SharedActivitiesJson = JsonSerializer.Serialize(
                activities.Select(a => new { a.GameId, a.ActivityName }).ToList()),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(link);
        return link;
    }

    public async Task UpdateLinkAsync(SharedActivityLink link)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(link);
    }

    public async Task DeactivateLinkAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var link = await conn.FindAsync<SharedActivityLink>(id);
        if (link == null) return;
        link.IsActive = false;
        await conn.UpdateAsync(link);
    }

    public static string GenerateLinkCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(8);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool VerifyPassword(string password, string storedHash) =>
        HashPassword(password) == storedHash;

    public List<(string GameId, string ActivityName)> GetSharedActivities(
        SharedActivityLink link)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<SharedActivityItem>>(
                link.SharedActivitiesJson) ?? new();
            return items.Select(i => (i.GameId, i.ActivityName)).ToList();
        }
        catch { return new(); }
    }

    private class SharedActivityItem
    {
        public string GameId { get; set; } = "";
        public string ActivityName { get; set; } = "";
    }

    public static byte[] Encrypt(string json, string password, string linkCode)
    {
        var key = DeriveKey(password, linkCode);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(
            ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            var data = Encoding.UTF8.GetBytes(json);
            cs.Write(data, 0, data.Length);
            cs.FlushFinalBlock();
        }
        return ms.ToArray();
    }

    public static string Decrypt(byte[] encrypted, string password,
        string linkCode)
    {
        var key = DeriveKey(password, linkCode);
        using var aes = Aes.Create();
        aes.Key = key;
        var iv = new byte[16];
        Array.Copy(encrypted, 0, iv, 0, 16);
        aes.IV = iv;
        var cipher = new byte[encrypted.Length - 16];
        Array.Copy(encrypted, 16, cipher, 0, cipher.Length);
        using var ms = new MemoryStream(cipher);
        using var cs = new CryptoStream(
            ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var sr = new StreamReader(cs, Encoding.UTF8);
        return sr.ReadToEnd();
    }

    private static byte[] DeriveKey(string password, string linkCode)
    {
        var salt = Encoding.UTF8.GetBytes(
            $"shared_act_{linkCode.ToUpperInvariant()}");
        using var rfc = new Rfc2898DeriveBytes(
            password, salt, 10000, HashAlgorithmName.SHA256);
        return rfc.GetBytes(32);
    }
}
