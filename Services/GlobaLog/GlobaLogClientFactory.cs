using FlightCrewStudio.Data;
using Microsoft.Extensions.Options;

namespace FlightCrewStudio.Services.GlobaLog;

public interface IGlobaLogClientFactory
{
    /// <summary>Build a client for the given stored credential, decrypting secrets on the way.</summary>
    IGlobaLogClient Create(GlobaLogCredential credential);
}

public sealed class GlobaLogClientFactory(IOptions<GlobaLogOptions> options, ICredentialProtector protector)
    : IGlobaLogClientFactory
{
    private readonly GlobaLogOptions _opt = options.Value;

    public IGlobaLogClient Create(GlobaLogCredential credential)
    {
        if (credential.AuthMode == GlobaLogAuthMode.ApiToken)
        {
            var token = protector.Unprotect(credential.ApiTokenCipher);
            if (string.IsNullOrWhiteSpace(token))
                throw new GlobaLogException("No GlobaLog API token saved. Add one in Settings.");
            return new TokenGlobaLogClient(_opt.BaseUrl, token, _opt.TimeoutSeconds);
        }

        var username = protector.Unprotect(credential.UsernameCipher);
        var password = protector.Unprotect(credential.PasswordCipher);
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new GlobaLogException("No GlobaLog username/password saved. Add them in Settings.");
        return new WebSessionGlobaLogClient(_opt.BaseUrl, username, password, _opt.TimeoutSeconds);
    }
}
