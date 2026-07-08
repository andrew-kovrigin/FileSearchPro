using System.Net;

namespace FileSearchPro.Services;

public class AuthService
{
    public NetworkCredential GetCredentials(bool useCurrentUser, string domain, string username, string password)
    {
        if (useCurrentUser)
            return CredentialCache.DefaultNetworkCredentials;

        if (string.IsNullOrEmpty(username))
            return CredentialCache.DefaultNetworkCredentials;

        return new NetworkCredential(username, password, domain);
    }
}
