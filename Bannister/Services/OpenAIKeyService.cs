using System.Security.Cryptography;
using System.Text;
using Microsoft.Maui.Storage;

namespace Bannister.Services;

public class OpenAIKeyService
{
    private const string MasterKeyStorageKey = "openai_master_encryption_key";
    private const string SourcePathStorageKey = "openai_key_source_path";
    private const int MasterKeyBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const int MaxKeyFileBytes = 10 * 1024;
    private readonly string _encryptedKeyPath = Path.Combine(FileSystem.AppDataDirectory, "openai_key.enc");

    public string? LastError { get; private set; }

    public async Task<bool> IsKeyConfiguredAsync()
    {
        try
        {
            var masterKey = await SecureStorage.GetAsync(MasterKeyStorageKey);
            return File.Exists(_encryptedKeyPath) && !string.IsNullOrWhiteSpace(masterKey);
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GetDecryptedKeyAsync()
    {
        LastError = null;

        try
        {
            if (!File.Exists(_encryptedKeyPath))
            {
                LastError = "Encrypted key file is missing.";
                return null;
            }

            var masterKey = await GetExistingMasterKeyAsync();
            if (masterKey == null)
            {
                LastError = "Master encryption key is missing.";
                return null;
            }

            var blob = await File.ReadAllBytesAsync(_encryptedKeyPath);
            if (blob.Length <= NonceBytes + TagBytes)
            {
                LastError = "Encrypted key file is corrupted.";
                return null;
            }

            var ciphertextLength = blob.Length - NonceBytes - TagBytes;
            var nonce = new byte[NonceBytes];
            var tag = new byte[TagBytes];
            var ciphertext = new byte[ciphertextLength];
            var plaintext = new byte[ciphertextLength];

            Buffer.BlockCopy(blob, 0, nonce, 0, NonceBytes);
            Buffer.BlockCopy(blob, NonceBytes, tag, 0, TagBytes);
            Buffer.BlockCopy(blob, NonceBytes + TagBytes, ciphertext, 0, ciphertextLength);

            using var aes = new AesGcm(masterKey, TagBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception ex)
        {
            LastError = $"Could not decrypt the stored key: {ex.Message}";
            return null;
        }
    }

    public async Task<bool> ConfigureFromFileAsync(string filePath)
    {
        return await ConfigureFromFileAsync(filePath, filePath);
    }

    public async Task<bool> ConfigureFromFileAsync(string filePath, string sourcePath)
    {
        LastError = null;

        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                LastError = "Selected key file could not be found.";
                return false;
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                LastError = "Selected key file is empty.";
                return false;
            }

            if (fileInfo.Length > MaxKeyFileBytes)
            {
                LastError = "Selected key file is too large. Choose a plain-text file under 10 KB.";
                return false;
            }

            var bytes = await File.ReadAllBytesAsync(filePath);
            if (bytes.Any(b => b == 0))
            {
                LastError = "Selected key file does not look like a plain-text key file.";
                return false;
            }

            var apiKey = DecodeUtf8(bytes).Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                LastError = "Selected key file is empty.";
                return false;
            }

            if (!apiKey.StartsWith("sk-", StringComparison.Ordinal))
            {
                LastError = "Selected file does not look like an OpenAI API key. The key should start with 'sk-'.";
                return false;
            }

            var masterKey = await GetOrCreateMasterKeyAsync();
            var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
            var plaintext = Encoding.UTF8.GetBytes(apiKey);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagBytes];

            using var aes = new AesGcm(masterKey, TagBytes);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

            var blob = new byte[NonceBytes + TagBytes + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, blob, 0, NonceBytes);
            Buffer.BlockCopy(tag, 0, blob, NonceBytes, TagBytes);
            Buffer.BlockCopy(ciphertext, 0, blob, NonceBytes + TagBytes, ciphertext.Length);

            await File.WriteAllBytesAsync(_encryptedKeyPath, blob);
            await SecureStorage.SetAsync(SourcePathStorageKey, sourcePath);
            return true;
        }
        catch (DecoderFallbackException)
        {
            LastError = "Selected key file is not valid UTF-8 text.";
            return false;
        }
        catch (Exception ex)
        {
            LastError = $"Could not configure the key: {ex.Message}";
            return false;
        }
    }

    public Task ClearAsync()
    {
        LastError = null;

        try
        {
            if (File.Exists(_encryptedKeyPath))
                File.Delete(_encryptedKeyPath);
        }
        catch (Exception ex)
        {
            LastError = $"Could not delete the encrypted key file: {ex.Message}";
        }

        SecureStorage.Remove(MasterKeyStorageKey);
        SecureStorage.Remove(SourcePathStorageKey);
        return Task.CompletedTask;
    }

    public async Task<string?> GetSourcePathAsync()
    {
        try
        {
            return await SecureStorage.GetAsync(SourcePathStorageKey);
        }
        catch
        {
            return null;
        }
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        var encoding = new UTF8Encoding(false, true);
        return encoding.GetString(bytes);
    }

    private static async Task<byte[]?> GetExistingMasterKeyAsync()
    {
        var base64 = await SecureStorage.GetAsync(MasterKeyStorageKey);
        if (string.IsNullOrWhiteSpace(base64))
            return null;

        var key = Convert.FromBase64String(base64);
        return key.Length == MasterKeyBytes ? key : null;
    }

    private static async Task<byte[]> GetOrCreateMasterKeyAsync()
    {
        var existing = await GetExistingMasterKeyAsync();
        if (existing != null)
            return existing;

        var key = RandomNumberGenerator.GetBytes(MasterKeyBytes);
        await SecureStorage.SetAsync(MasterKeyStorageKey, Convert.ToBase64String(key));
        return key;
    }
}
