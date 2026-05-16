using System.Security.Cryptography;
using System.Text;

namespace Bannister.Services;

public static class QueuePayloadCrypto
{
    private const string Salt = "bannister-queue-v1";
    private const int Iterations = 50000;
    private const int KeyBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    public static string EncryptPayload(string plaintext, string loginPassword)
    {
        if (string.IsNullOrEmpty(loginPassword))
            throw new InvalidOperationException("Cannot encrypt queued operation without a login password.");

        if (!AesGcm.IsSupported)
            throw new PlatformNotSupportedException("AES-GCM is not supported on this platform.");

        var key = DeriveKey(loginPassword);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagBytes];

        using var aes = new AesGcm(key, TagBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var output = new byte[NonceBytes + ciphertext.Length + TagBytes];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceBytes);
        Buffer.BlockCopy(ciphertext, 0, output, NonceBytes, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, output, NonceBytes + ciphertext.Length, TagBytes);

        return Convert.ToBase64String(output);
    }

    public static string DecryptPayload(string base64Encrypted, string loginPassword)
    {
        if (string.IsNullOrEmpty(loginPassword))
            throw new InvalidOperationException("Cannot decrypt queued operation without a login password.");

        if (!AesGcm.IsSupported)
            throw new PlatformNotSupportedException("AES-GCM is not supported on this platform.");

        var input = Convert.FromBase64String(base64Encrypted);
        if (input.Length < NonceBytes + TagBytes)
            throw new CryptographicException("Encrypted payload is too short.");

        var ciphertextLength = input.Length - NonceBytes - TagBytes;
        var nonce = new byte[NonceBytes];
        var ciphertext = new byte[ciphertextLength];
        var tag = new byte[TagBytes];
        var plaintext = new byte[ciphertextLength];

        Buffer.BlockCopy(input, 0, nonce, 0, NonceBytes);
        Buffer.BlockCopy(input, NonceBytes, ciphertext, 0, ciphertextLength);
        Buffer.BlockCopy(input, NonceBytes + ciphertextLength, tag, 0, TagBytes);

        var key = DeriveKey(loginPassword);
        using var aes = new AesGcm(key, TagBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    private static byte[] DeriveKey(string loginPassword)
    {
        var saltBytes = Encoding.UTF8.GetBytes(Salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(
            loginPassword,
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(KeyBytes);
    }
}
