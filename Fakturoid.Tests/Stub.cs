using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fakturoid.Tests
{
    // A request as the stub received it (the content is read before the handler answers).
    public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Authorization, string UserAgent, string Accept, string ContentType, string Body)
    {
        public string PathAndQuery => Uri.PathAndQuery;
        public bool IsTokenRequest => Uri.AbsolutePath.EndsWith("/oauth/token");
    }

    // HttpMessageHandler for offline tests: answers the token endpoint itself (tokens "token-1", "token-2", ...)
    // and every other request with the next queued response.
    public sealed class StubHandler : HttpMessageHandler
    {
        private readonly ConcurrentQueue<Func<RecordedRequest, CancellationToken, Task<HttpResponseMessage>>> responses = new();
        private int tokens;

        public List<RecordedRequest> Requests { get; } = new();

        public IEnumerable<RecordedRequest> ApiRequests => Requests.Where(r => !r.IsTokenRequest);

        public int TokenRequests => Requests.Count(r => r.IsTokenRequest);

        // Token response; replace for token tests.
        public Func<int, HttpResponseMessage> TokenResponse { get; set; } = n => Json(HttpStatusCode.OK,
            $"{{\"access_token\":\"token-{n}\",\"token_type\":\"Bearer\",\"expires_in\":7200}}");

        // Delay before the token endpoint answers (observes the token).
        public TimeSpan TokenDelay { get; set; } = TimeSpan.Zero;

        public StubHandler Enqueue(Func<RecordedRequest, CancellationToken, Task<HttpResponseMessage>> response)
        {
            responses.Enqueue(response);
            return this;
        }

        public StubHandler Enqueue(Func<RecordedRequest, HttpResponseMessage> response)
        {
            return Enqueue((request, ct) => Task.FromResult(response(request)));
        }

        public StubHandler Enqueue(HttpResponseMessage response)
        {
            return Enqueue(_ => response);
        }

        public StubHandler EnqueueJson(HttpStatusCode status, string json, params (string Name, string Value)[] headers)
        {
            return Enqueue(_ =>
            {
                var response = Json(status, json);
                foreach (var header in headers)
                {
                    response.Headers.TryAddWithoutValidation(header.Name, header.Value);
                }
                return response;
            });
        }

        public StubHandler EnqueueStatus(HttpStatusCode status)
        {
            return Enqueue(_ => new HttpResponseMessage(status));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var recorded = new RecordedRequest(request.Method, request.RequestUri,
                request.Headers.TryGetValues("Authorization", out var auth) ? string.Join(",", auth) : null,
                request.Headers.TryGetValues("User-Agent", out var ua) ? string.Join(" ", ua) : null,
                request.Headers.TryGetValues("Accept", out var accept) ? string.Join(",", accept) : null,
                request.Content?.Headers.ContentType?.ToString(),
                body);
            lock (Requests)
            {
                Requests.Add(recorded);
            }
            if (recorded.IsTokenRequest)
            {
                if (TokenDelay > TimeSpan.Zero)
                {
                    await Task.Delay(TokenDelay, cancellationToken);
                }
                return TokenResponse(Interlocked.Increment(ref tokens));
            }
            if (!responses.TryDequeue(out var next))
            {
                throw new InvalidOperationException($"No response queued for {request.Method} {request.RequestUri}");
            }
            var response = await next(recorded, cancellationToken);
            response.RequestMessage = request;
            return response;
        }

        public static HttpResponseMessage Json(HttpStatusCode status, string json)
        {
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    // A response body that sends nothing until cancelled: proves the token reaches the body read.
    public sealed class EndlessStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Synchronous read");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    public static class TestClient
    {
        public const string Slug = "applecorp";
        public const string ClientId = "test-client-id";
        public const string ClientSecret = "test-client-secret-do-not-leak";

        public static Birko.Fakturoid.Fakturoid Create(StubHandler handler, string slug = Slug)
        {
            return new Birko.Fakturoid.Fakturoid(ClientId, ClientSecret, slug, "FakturoidTests", "tests@example.com", new HttpClient(handler));
        }

        public static string Data(string file)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", file));
        }
    }
}
