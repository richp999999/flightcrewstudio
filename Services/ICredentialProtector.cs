namespace FlightCrewStudio.Services;

/// <summary>
/// Encrypts/decrypts small secret strings (GlobaLog username, password, API token) using a
/// symmetric key held on the server. The database only ever stores the ciphertext returned by
/// <see cref="Protect"/>.
/// </summary>
public interface ICredentialProtector
{
    /// <summary>Encrypt plaintext, returning an opaque, storable string. Null/empty passes through as null.</summary>
    string? Protect(string? plaintext);

    /// <summary>Decrypt a value produced by <see cref="Protect"/>. Null/empty passes through as null.</summary>
    string? Unprotect(string? ciphertext);
}
