using Birko.Fakturoid.Exceptions;
using Birko.Fakturoid.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Birko.Fakturoid.Authentication
{
    // Gets and caches the OAuth access token (POST https://app.fakturoid.cz/api/v3/oauth/token).
    // An access token lives 2 hours (expires_in); a new one is fetched RefreshBefore its expiry.
    // Subclasses define the grant: ClientCredentialsTokenProvider now; an Authorization Code provider
    // (authorization_code + refresh_token grants, see OnTokenReceived) can be added without changing the client.
    // One provider instance may be shared by several Fakturoid instances of the same credentials.
    public abstract class OAuthTokenProvider
    {
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private CachedToken cached;

        protected OAuthTokenProvider(string clientId, string clientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("The OAuth client id is required.", nameof(clientId));
            }
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new ArgumentException("The OAuth client secret is required.", nameof(clientSecret));
            }
            ClientId = clientId;
            ClientSecret = clientSecret;
        }

        public string ClientId { get; }

        // Never logged or put into exceptions and events.
        protected string ClientSecret { get; }

        // How long before its expiry a cached token is replaced (at most half of the token lifetime).
        public TimeSpan RefreshBefore { get; set; } = TimeSpan.FromMinutes(5);

        // When the cached token expires, null when there is none. The token itself is not exposed.
        public DateTimeOffset? TokenExpiresAt => Volatile.Read(ref cached)?.ExpiresAt;

        // Replaceable clock for tests.
        internal Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

        // Drops the cached token, the next request fetches a new one.
        public void Invalidate()
        {
            Volatile.Write(ref cached, null);
        }

        // The body of the token request, e.g. {"grant_type": "client_credentials"}.
        protected abstract object TokenRequestBody();

        // Called with every token response, e.g. to keep the refresh_token of the Authorization Code flow.
        protected virtual void OnTokenReceived(TokenResponse response)
        {
        }

        // rejectedToken: the token the API has just refused with 401; it is never returned again.
        internal async Task<IssuedToken> GetToken(Fakturoid client, string rejectedToken, CancellationToken cancellationToken)
        {
            var token = Volatile.Read(ref cached);
            if (IsUsable(token, rejectedToken))
            {
                return new IssuedToken(token.Value, token.Type, true);
            }
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Another caller may have fetched a token while this one was waiting.
                token = Volatile.Read(ref cached);
                if (IsUsable(token, rejectedToken))
                {
                    return new IssuedToken(token.Value, token.Type, true);
                }
                token = await RequestToken(client, cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref cached, token);
                return new IssuedToken(token.Value, token.Type, false);
            }
            finally
            {
                gate.Release();
            }
        }

        private bool IsUsable(CachedToken token, string rejectedToken)
        {
            if (token == null || (rejectedToken != null && token.Value == rejectedToken))
            {
                return false;
            }
            var margin = RefreshBefore;
            var half = TimeSpan.FromTicks((token.ExpiresAt - token.IssuedAt).Ticks / 2);
            if (margin > half)
            {
                margin = half;
            }
            return Clock() < token.ExpiresAt - margin;
        }

        private async Task<CachedToken> RequestToken(Fakturoid client, CancellationToken cancellationToken)
        {
            var issuedAt = Clock();
            var content = new StringContent(FakturoidJson.Serialize(TokenRequestBody()), Encoding.UTF8, "application/json");
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(ClientId + ":" + ClientSecret));
            var raw = await client.Execute(HttpMethod.Post, "oauth/token", null, content, "Basic " + basic, "application/json", true, cancellationToken).ConfigureAwait(false);
            TokenResponse response;
            try
            {
                response = FakturoidJson.Deserialize<TokenResponse>(raw.Body);
            }
            catch (System.Text.Json.JsonException ex)
            {
                // The body may hold a token: never put it into the exception.
                throw new ParseException("The OAuth token response is not the expected JSON.", ex, (int)raw.StatusCode, raw.Method, raw.Path, null);
            }
            if (response == null || string.IsNullOrEmpty(response.AccessToken))
            {
                throw new ParseException("The OAuth token response has no access_token.", null, (int)raw.StatusCode, raw.Method, raw.Path, null);
            }
            OnTokenReceived(response);
            var lifetime = TimeSpan.FromSeconds(response.ExpiresIn > 0 ? response.ExpiresIn : 7200);
            return new CachedToken(response.AccessToken, string.IsNullOrEmpty(response.TokenType) ? "Bearer" : response.TokenType, issuedAt, issuedAt + lifetime);
        }

        private sealed class CachedToken
        {
            public CachedToken(string value, string type, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
            {
                Value = value;
                Type = type;
                IssuedAt = issuedAt;
                ExpiresAt = expiresAt;
            }

            public string Value { get; }
            public string Type { get; }
            public DateTimeOffset IssuedAt { get; }
            public DateTimeOffset ExpiresAt { get; }
        }
    }

    internal struct IssuedToken
    {
        public IssuedToken(string value, string type, bool fromCache)
        {
            Value = value;
            Type = type;
            FromCache = fromCache;
        }

        public string Value { get; }
        public string Type { get; }
        public bool FromCache { get; }

        // "Bearer <token>" (Authorization: token_type access_token).
        public string AuthorizationHeader => Type + " " + Value;
    }

    // Answer of the token endpoint.
    public class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        // Seconds, 7200 (2 hours).
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        // Authorization Code flow only; the Client Credentials flow has no refresh token.
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        public override string ToString()
        {
            // Never print the tokens.
            return string.Format("TokenResponse {{ token_type = {0}, expires_in = {1} }}", TokenType, ExpiresIn);
        }
    }
}
