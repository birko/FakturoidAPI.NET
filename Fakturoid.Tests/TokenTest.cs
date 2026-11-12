using Birko.Fakturoid.Exceptions;
using Shouldly;
using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Fakturoid.Tests
{
    // Offline: OAuth Client Credentials token request, caching and expiry.
    public class TokenTest
    {
        private static (Birko.Fakturoid.Fakturoid Client, StubHandler Handler, Func<DateTimeOffset> Now, Action<TimeSpan> Advance) Create(int responses)
        {
            var handler = new StubHandler();
            for (var i = 0; i < responses; i++)
            {
                handler.EnqueueJson(HttpStatusCode.OK, TestClient.Data("account.json"));
            }
            var client = TestClient.Create(handler);
            var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            client.TokenProvider.Clock = () => now;
            return (client, handler, () => now, by => now += by);
        }

        [Fact]
        public async Task TokenRequestUsesBasicAuthAndClientCredentialsGrant()
        {
            var (client, handler, _, _) = Create(1);

            await client.Account.View();

            var token = handler.Requests.First(r => r.IsTokenRequest);
            token.Method.Method.ShouldBe("POST");
            token.Uri.ToString().ShouldBe("https://app.fakturoid.cz/api/v3/oauth/token");
            token.Authorization.ShouldBe("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(TestClient.ClientId + ":" + TestClient.ClientSecret)));
            token.Body.ShouldBe("{\"grant_type\":\"client_credentials\"}");
            token.ContentType.ShouldBe("application/json; charset=utf-8");
            token.UserAgent.ShouldBe("FakturoidTests (tests@example.com)");
        }

        [Fact]
        public async Task TokenIsCachedUntilShortlyBeforeExpiry()
        {
            var (client, handler, now, advance) = Create(4);

            await client.Account.View();
            advance(TimeSpan.FromMinutes(100));
            await client.Account.View();
            client.TokenProvider.TokenExpiresAt.ShouldBe(now().AddMinutes(20));
            // 2 hours minus RefreshBefore (5 minutes): replaced before it expires.
            advance(TimeSpan.FromMinutes(16));
            await client.Account.View();
            await client.Account.View();

            handler.TokenRequests.ShouldBe(2);
            handler.ApiRequests.Select(r => r.Authorization).ShouldBe(new[] { "Bearer token-1", "Bearer token-1", "Bearer token-2", "Bearer token-2" });
        }

        [Fact]
        public async Task ShortLivedTokenIsReplacedAtHalfItsLifetime()
        {
            var (client, handler, _, advance) = Create(2);
            handler.TokenResponse = n => StubHandler.Json(HttpStatusCode.OK, $"{{\"access_token\":\"short-{n}\",\"token_type\":\"Bearer\",\"expires_in\":120}}");

            await client.Account.View();
            advance(TimeSpan.FromSeconds(61));
            await client.Account.View();

            handler.TokenRequests.ShouldBe(2);
        }

        [Fact]
        public async Task ConcurrentCallsShareOneTokenRequest()
        {
            var (client, handler, _, _) = Create(10);
            handler.TokenDelay = TimeSpan.FromMilliseconds(100);

            await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => client.Account.View()));

            handler.TokenRequests.ShouldBe(1);
        }

        [Fact]
        public async Task RejectedTokenIsReplacedOnce()
        {
            var handler = new StubHandler()
                .EnqueueJson(HttpStatusCode.OK, TestClient.Data("account.json"))
                .EnqueueJson(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_token\",\"error_description\":\"The access token is invalid\"}")
                .EnqueueJson(HttpStatusCode.OK, TestClient.Data("account.json"));
            var client = TestClient.Create(handler);

            await client.Account.View();
            await client.Account.View();

            handler.TokenRequests.ShouldBe(2);
            handler.ApiRequests.Select(r => r.Authorization).ShouldBe(new[] { "Bearer token-1", "Bearer token-1", "Bearer token-2" });
        }

        [Fact]
        public async Task FreshTokenRejectedIsNotRetried()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_token\",\"error_description\":\"No access\"}");
            var client = TestClient.Create(handler);

            await Should.ThrowAsync<AuthenticationException>(() => client.Account.View());

            handler.TokenRequests.ShouldBe(1);
            handler.ApiRequests.Count().ShouldBe(1);
        }

        [Fact]
        public async Task WrongCredentialsAreAnAuthenticationException()
        {
            var handler = new StubHandler
            {
                TokenResponse = _ => StubHandler.Json(HttpStatusCode.BadRequest, "{\"error\":\"invalid_client\",\"error_description\":\"Client authentication failed\"}"),
            };
            var client = TestClient.Create(handler);

            var ex = await Should.ThrowAsync<AuthenticationException>(() => client.Account.View());

            ex.Error.ShouldBe("invalid_client");
            ex.Uri.ShouldBe("oauth/token");
            ex.ResponseBody.ShouldBeNull();
            ex.ToString().ShouldNotContain(TestClient.ClientSecret);
            handler.ApiRequests.ShouldBeEmpty();
        }

        [Fact]
        public async Task InvalidateFetchesANewToken()
        {
            var (client, handler, _, _) = Create(2);

            await client.Account.View();
            client.TokenProvider.Invalidate();
            await client.Account.View();

            handler.TokenRequests.ShouldBe(2);
        }

        [Fact]
        public async Task TokenResponseWithoutTokenIsAParseExceptionWithoutTheBody()
        {
            var handler = new StubHandler { TokenResponse = _ => StubHandler.Json(HttpStatusCode.OK, "{\"token_type\":\"Bearer\"}") };
            var client = TestClient.Create(handler);

            var ex = await Should.ThrowAsync<ParseException>(() => client.Account.View());

            ex.ResponseExcerpt.ShouldBeNull();
        }
    }
}
