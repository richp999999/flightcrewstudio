using System.Security.Cryptography;
using System.Text;

namespace FlightCrewStudio.Services;

/// <summary>
/// AES-256-GCM implementation of <see cref="ICredentialProtector"/>.
///
/// The key is held on the server (never in the database). It is sourced, in order of
/// preference, from:
///   1. configuration value <c>Encryption:Key</c> (base64 of 32 bytes), or
///   2. a key file at <c>Encryption:KeyFilePath</c>. If that file is missing, a fresh random
///      key is generated and written there (0600) so it is stable across restarts.
///
/// Stored format is base64 of: [12-byte nonce][16-byte tag][ciphertext].
/// </summary>
public sealed class AesGcmCredentialProtector : ICredentialProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesGcmCredentialProtector(IConfiguration config, ILogger<AesGcmCredentialProtector> logger)
    {
        var b64 = config["Encryption:Key"];
        if (!string.IsNullOrWhiteSpace(b64))
        {
            _key = Convert.FromBase64String(b64.Trim());
            if (_key.Length != 32)
                throw new InvalidOperationException("Encryption:Key must be base64 of exactly 32 bytes (AES-256).");
            logger.LogInformation("Credential encryption key loaded from configuration.");
            return;
        }

        var path = config["Encryption:KeyFilePath"] ?? "/keys/credential-key";
        if (File.Exists(path))
        {
            _key = Convert.FromBase64String(File.ReadAllText(path).Trim());
            if (_key.Length != 32)
                throw new InvalidOperationException($"Key file '{path}' must contain base64 of exactly 32 bytes.");
            logger.LogInformation("Credential encryption key loaded from {Path}.", path);
            return;
        }

        // Generate and persist a new key.
        _key = RandomNumberGenerator.GetBytes(32);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, Convert.ToBase64String(_key));
        TryRestrictPermissions(path);
        logger.LogWarning("No encryption key configured; generated a new key at {Path}. Keep this file safe and backed up.", path);
    }

    public string? Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return null;

        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        var output = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize + TagSize, cipher.Length);
        return Convert.ToBase64String(output);
    }

    public string? Unprotect(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return null;

        var input = Convert.FromBase64String(ciphertext);
        if (input.Length < NonceSize + TagSize)
            throw new CryptographicException("Ciphertext too short.");

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipher = new byte[input.Length - NonceSize - TagSize];
        Buffer.BlockCopy(input, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(input, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(input, NonceSize + TagSize, cipher, 0, cipher.Length);

        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private static void TryRestrictPermissions(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch { /* best effort */ }
    }
}
