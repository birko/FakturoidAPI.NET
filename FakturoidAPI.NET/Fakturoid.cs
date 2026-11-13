using Birko.Fakturoid.Authentication;
using Birko.Fakturoid.Exceptions;
using Birko.Fakturoid.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Birko.Fakturoid
{
    // Client of the Fakturoid API v3 (https://www.fakturoid.cz/api/v3).
    // The library code keeps to C# 7.3, so the shared project builds in any project that imports it.
    public class Fakturoid
    {
        public const string DefaultApiUrl = "https://app.fakturoid.cz/api/v3/";

        // Records per page of every list endpoint (API introduction > Pagination).
        public const int PageSize = 40;

        // One HttpClient for all instances that were not given their own; timeouts are per request (Timeout).
        private static readonly HttpClient SharedHttpClient = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

        private readonly HttpClient httpClient;
        private readonly Uri apiUri;
        private RateLimit rateLimit;

        private Users users;
        private Accounts account;
        private Subjects subjects;
        private Invoices invoices;
        private InvoicePayments invoicePayments;
        private Webhooks webhooks;
        private NumberFormats numberFormats;

        // Client Credentials Flow: client id and secret from the Fakturoid user's Settings > User account.
        // applicationName and contactEmail form the mandatory User-Agent "applicationName (contactEmail)";
        // Fakturoid answers 400 without it. accountSlug can stay null until an account endpoint is called
        // (Users.Current lists the accounts of the user).
        public Fakturoid(string clientId, string clientSecret, string accountSlug, string applicationName, string contactEmail, HttpClient httpClient = null)
            : this(new ClientCredentialsTokenProvider(clientId, clientSecret), accountSlug, applicationName, contactEmail, httpClient)
        {
        }

        // Any OAuth flow, e.g. a future Authorization Code provider.
        public Fakturoid(OAuthTokenProvider tokenProvider, string accountSlug, string applicationName, string contactEmail, HttpClient httpClient = null, string apiUrl = DefaultApiUrl)
        {
            if (string.IsNullOrWhiteSpace(applicationName))
            {
                throw new ArgumentException("The application name is required for the User-Agent header.", nameof(applicationName));
            }
            if (string.IsNullOrWhiteSpace(contactEmail) || !contactEmail.Contains("@"))
            {
                throw new ArgumentException("A contact e-mail is required for the User-Agent header.", nameof(contactEmail));
            }
            TokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            AccountSlug = accountSlug;
            UserAgent = string.Format("{0} ({1})", applicationName.Trim(), contactEmail.Trim());
            this.httpClient = httpClient ?? SharedHttpClient;
            var url = string.IsNullOrEmpty(apiUrl) ? DefaultApiUrl : apiUrl;
            apiUri = new Uri(url.EndsWith("/") ? url : url + "/", UriKind.Absolute);
        }

        public OAuthTokenProvider TokenProvider { get; }

        // The account the account endpoints work with ("slug" in https://app.fakturoid.cz/api/v3/accounts/{slug}/...).
        public string AccountSlug { get; set; }

        public string UserAgent { get; }

        public Uri ApiUrl => apiUri;

        // Time limit of one HTTP request (including reading the response). When it runs out the call throws
        // TimeoutException; a cancelled CancellationToken throws OperationCanceledException instead.
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        // Adds request and response bodies to RequestCompleted. Off by default: bodies contain personal data.
        public bool IncludeBodiesInEvents { get; set; } = false;

        // Rate limit from the headers of the last response that had them, or null before the first one.
        public RateLimit RateLimit => Volatile.Read(ref rateLimit);

        // Fired after every HTTP request (also failed and cancelled ones), for debugging and monitoring.
        // Never carries headers (Authorization holds the access token); see RequestCompletedEventArgs.
        public event EventHandler<RequestCompletedEventArgs> RequestCompleted;

        public Users Users => users ?? (users = new Users(this));

        public Accounts Account => account ?? (account = new Accounts(this));

        public Subjects Subjects => subjects ?? (subjects = new Subjects(this));

        public Invoices Invoices => invoices ?? (invoices = new Invoices(this));

        public InvoicePayments InvoicePayments => invoicePayments ?? (invoicePayments = new InvoicePayments(this));

        public Webhooks Webhooks => webhooks ?? (webhooks = new Webhooks(this));

        public NumberFormats NumberFormats => numberFormats ?? (numberFormats = new NumberFormats(this));

        internal string AccountPath(string path)
        {
            if (string.IsNullOrWhiteSpace(AccountSlug))
            {
                throw new InvalidOperationException("AccountSlug is not set. Users.Current() lists the accounts of the user.");
            }
            return string.Format("accounts/{0}/{1}", Uri.EscapeDataString(AccountSlug), path);
        }

        internal async Task<T> Get<T>(string path, CancellationToken cancellationToken)
        {
            var response = await SendAuthorized(HttpMethod.Get, path, null, JsonMediaType, cancellationToken).ConfigureAwait(false);
            return response.Deserialize<T>();
        }

        internal async Task<Page<T>> GetPage<T>(string path, int page, CancellationToken cancellationToken)
        {
            if (page < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(page), "Pages are numbered from 1.");
            }
            var items = await Get<List<T>>(path, cancellationToken).ConfigureAwait(false);
            return new Page<T>(page, items ?? new List<T>());
        }

        // All pages: Fakturoid has no total count, the end is an empty page (API changelog, v3 changes).
        internal async Task<List<T>> GetAllPages<T>(Func<int, string> pathOfPage, int maxPages, CancellationToken cancellationToken)
        {
            if (maxPages < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPages));
            }
            var all = new List<T>();
            for (var page = 1; page <= maxPages; page++)
            {
                var items = await GetPage<T>(pathOfPage(page), page, cancellationToken).ConfigureAwait(false);
                if (items.Items.Count == 0)
                {
                    break;
                }
                all.AddRange(items.Items);
            }
            return all;
        }

        internal async Task<TResult> Send<TBody, TResult>(HttpMethod method, string path, TBody body, CancellationToken cancellationToken)
        {
            var response = await SendAuthorized(method, path, FakturoidJson.Serialize(body), JsonMediaType, cancellationToken).ConfigureAwait(false);
            return response.Deserialize<TResult>();
        }

        internal Task SendWithoutResult(HttpMethod method, string path, CancellationToken cancellationToken)
        {
            return SendAuthorized(method, path, null, JsonMediaType, cancellationToken);
        }

        internal Task<RawResponse> GetBinary(string path, string accept, CancellationToken cancellationToken)
        {
            return SendAuthorized(HttpMethod.Get, path, null, accept, cancellationToken);
        }

        private const string JsonMediaType = "application/json";

        // Every API call: a cached access token; a 401 with a cached token fetches a new one and repeats the
        // request once (the API refused it before doing anything, so this is safe also for POST).
        private async Task<RawResponse> SendAuthorized(HttpMethod method, string path, string json, string accept, CancellationToken cancellationToken)
        {
            var token = await TokenProvider.GetToken(this, null, cancellationToken).ConfigureAwait(false);
            try
            {
                return await Execute(method, path, json, null, token.AuthorizationHeader, accept, false, cancellationToken).ConfigureAwait(false);
            }
            catch (AuthenticationException) when (token.FromCache)
            {
                token = await TokenProvider.GetToken(this, token.Value, cancellationToken).ConfigureAwait(false);
                return await Execute(method, path, json, null, token.AuthorizationHeader, accept, false, cancellationToken).ConfigureAwait(false);
            }
        }

        // The single place every HTTP request goes through: User-Agent, timeout, cancellation, rate limit
        // headers, error mapping and RequestCompleted. secret = the bodies hold credentials (token endpoint)
        // and are never put into RequestCompleted.
        internal async Task<RawResponse> Execute(HttpMethod method, string path, string json, HttpContent content, string authorization,
            string accept, bool secret, CancellationToken cancellationToken)
        {
            // Cancelled before anything was sent: no request and no RequestCompleted.
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            RawResponse raw = null;
            int? statusCode = null;
            Exception error = null;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (Timeout > TimeSpan.Zero && Timeout != System.Threading.Timeout.InfiniteTimeSpan)
                {
                    timeout.CancelAfter(Timeout);
                }
                try
                {
                    using (var request = new HttpRequestMessage(method, new Uri(apiUri, path)))
                    {
                        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
                        request.Headers.TryAddWithoutValidation("Accept", accept);
                        if (authorization != null)
                        {
                            request.Headers.TryAddWithoutValidation("Authorization", authorization);
                        }
                        if (content != null)
                        {
                            request.Content = content;
                        }
                        else if (json != null)
                        {
                            request.Content = new StringContent(json, Encoding.UTF8, JsonMediaType);
                        }

                        using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                        {
                            statusCode = (int)response.StatusCode;
                            var limit = RateLimit.FromHeaders(response.Headers, DateTimeOffset.UtcNow);
                            if (limit != null)
                            {
                                Volatile.Write(ref rateLimit, limit);
                            }
                            var body = await ReadBody(response, timeout.Token).ConfigureAwait(false);
                            raw = new RawResponse(response.StatusCode, response.Content?.Headers?.ContentType?.MediaType, response.Headers.Location, body, method.Method, path);
                            if (!response.IsSuccessStatusCode)
                            {
                                throw ErrorMapper.Create(raw, limit, response.Headers.RetryAfter, secret, DateTimeOffset.UtcNow);
                            }
                            return raw;
                        }
                    }
                }
                catch (Exception ex) when (cancellationToken.IsCancellationRequested && !(ex is FakturoidException))
                {
                    // The caller's token: always OperationCanceledException carrying that token (HttpClient
                    // reports the internal linked one), never an API exception.
                    var canceled = ex as OperationCanceledException;
                    if (canceled != null && canceled.CancellationToken == cancellationToken)
                    {
                        error = ex;
                        throw;
                    }
                    error = new OperationCanceledException("The request was cancelled.", ex, cancellationToken);
                    throw error;
                }
                catch (Exception ex) when (timeout.IsCancellationRequested && !(ex is FakturoidException))
                {
                    error = new TimeoutException(string.Format("Fakturoid did not answer {0} {1} within {2}.", method.Method, path, Timeout), ex);
                    throw error;
                }
                catch (Exception ex)
                {
                    error = ex;
                    throw;
                }
                finally
                {
                    OnRequestCompleted(method.Method, path, json, raw, statusCode, error, stopwatch.Elapsed, secret);
                }
            }
        }

        // The token is passed to the body read as well (CopyToAsync); disposing the response on
        // cancellation also aborts a read that does not observe the token on older frameworks.
        private static async Task<byte[]> ReadBody(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.Content == null)
            {
                return new byte[0];
            }
            using (cancellationToken.Register(state => ((HttpResponseMessage)state).Dispose(), response))
            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory, 81920, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return memory.ToArray();
            }
        }

        private void OnRequestCompleted(string method, string path, string requestBody, RawResponse raw, int? statusCode, Exception error, TimeSpan duration, bool secret)
        {
            var handlers = RequestCompleted;
            if (handlers == null)
            {
                return;
            }
            var includeBodies = IncludeBodiesInEvents && !secret;
            string responseBody = null;
            if (includeBodies && raw != null && !raw.IsBinary)
            {
                responseBody = Secrets.HideInJson(raw.Text);
            }
            var args = new RequestCompletedEventArgs(method, path, statusCode, duration,
                includeBodies ? Secrets.HideInJson(requestBody) : null,
                responseBody,
                raw?.Body.Length,
                error);
            foreach (EventHandler<RequestCompletedEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception)
                {
                    // A failing handler must not change the result of the API call (e.g. after an invoice
                    // was already created), nor stop the other handlers.
                }
            }
        }
    }

    // A finished HTTP response, already read into memory.
    internal sealed class RawResponse
    {
        public RawResponse(HttpStatusCode statusCode, string mediaType, Uri location, byte[] body, string method, string path)
        {
            StatusCode = statusCode;
            MediaType = mediaType;
            Location = location;
            Body = body ?? new byte[0];
            Method = method;
            Path = path;
        }

        public HttpStatusCode StatusCode { get; }
        public string MediaType { get; }
        public Uri Location { get; }
        public byte[] Body { get; }
        public string Method { get; }
        public string Path { get; }

        public bool IsBinary => MediaType != null && !MediaType.Contains("json") && !MediaType.StartsWith("text/");

        public string Text => Encoding.UTF8.GetString(Body);

        public T Deserialize<T>()
        {
            if (Body.Length == 0)
            {
                throw new ParseException(string.Format("Fakturoid answered {0} {1} with {2} and an empty body where JSON was expected.",
                    Method, Path, (int)StatusCode), null, (int)StatusCode, Method, Path, null);
            }
            try
            {
                return FakturoidJson.Deserialize<T>(Body);
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new ParseException(string.Format("The response to {0} {1} ({2}) is not the expected JSON: {3}",
                    Method, Path, (int)StatusCode, ex.Message), ex, (int)StatusCode, Method, Path, Secrets.Excerpt(Text));
            }
        }
    }
}
