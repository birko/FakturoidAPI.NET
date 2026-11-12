using System.Collections.Generic;

namespace Birko.Fakturoid.Authentication
{
    // OAuth 2 Client Credentials Flow: client id and secret from the user's Settings > User account in Fakturoid.
    // There is no refresh token; a new access token is requested shortly before the old one expires.
    // Fakturoid allows this flow for access to your own account only, not for multi-tenant applications.
    public class ClientCredentialsTokenProvider : OAuthTokenProvider
    {
        public ClientCredentialsTokenProvider(string clientId, string clientSecret)
            : base(clientId, clientSecret)
        {
        }

        protected override object TokenRequestBody()
        {
            return new Dictionary<string, string> { { "grant_type", "client_credentials" } };
        }
    }
}
