using Birko.Fakturoid.Exceptions;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fakturoid.Tests
{
    // Offline: a cancelled CancellationToken aborts the request and surfaces as OperationCanceledException,
    // never as a Fakturoid exception; the library's own Timeout is a TimeoutException.
    public class CancellationTest
    {
        // A local HTTP server: answers the token endpoint, accepts every other request and never answers it.
        private sealed class SilentServer : IDisposable
        {
            private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource stop = new CancellationTokenSource();
            private readonly List<TcpClient> connections = new List<TcpClient>();
            private readonly TaskCompletionSource<TcpClient> silent = new TaskCompletionSource<TcpClient>(TaskCreationOptions.RunContinuationsAsynchronously);

            public SilentServer()
            {
                listener.Start();
                _ = AcceptLoop();
            }

            public string Url => $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/api/v3/";

            // The connection of the first API request the server keeps silent on.
            public Task<TcpClient> SilentConnection => silent.Task;

            private async Task AcceptLoop()
            {
                try
                {
                    while (!stop.IsCancellationRequested)
                    {
                        var connection = await listener.AcceptTcpClientAsync(stop.Token);
                        lock (connections)
                        {
                            connections.Add(connection);
                        }
                        _ = Serve(connection);
                    }
                }
                catch (Exception)
                {
                    // Stopped.
                }
            }

            private async Task Serve(TcpClient connection)
            {
                var stream = connection.GetStream();
                var buffer = new byte[8192];
                var received = new StringBuilder();
                while (true)
                {
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length, stop.Token);
                    if (read == 0)
                    {
                        return;
                    }
                    received.Append(Encoding.ASCII.GetString(buffer, 0, read));
                    var text = received.ToString();
                    if (!text.Contains("\r\n\r\n"))
                    {
                        continue;
                    }
                    if (text.StartsWith("POST /api/v3/oauth/token"))
                    {
                        var body = "{\"access_token\":\"local-token\",\"token_type\":\"Bearer\",\"expires_in\":7200}";
                        var answer = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nConnection: close\r\nContent-Length: " + body.Length + "\r\n\r\n" + body;
                        var bytes = Encoding.ASCII.GetBytes(answer);
                        await stream.WriteAsync(bytes, 0, bytes.Length, stop.Token);
                        connection.Client.Shutdown(SocketShutdown.Send);
                        return;
                    }
                    silent.TrySetResult(connection);
                    return; // Read nothing more, answer nothing.
                }
            }

            // True when the client closed the connection: the HTTP request was aborted, not just abandoned.
            public static async Task<bool> ClosedByClient(TcpClient connection, TimeSpan within)
            {
                var stream = connection.GetStream();
                var buffer = new byte[4096];
                using var timeout = new CancellationTokenSource(within);
                try
                {
                    while (await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token) > 0)
                    {
                        // Skip whatever is left of the request.
                    }
                    return true;
                }
                catch (IOException)
                {
                    return true; // Connection reset.
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }

            public void Dispose()
            {
                stop.Cancel();
                listener.Stop();
                lock (connections)
                {
                    connections.ForEach(c => c.Dispose());
                }
            }
        }

        // A call that ignores the token would hang: fail after 5 seconds instead.
        private static Task Bounded(Task call) => call.WaitAsync(TimeSpan.FromSeconds(5));

        private static Birko.Fakturoid.Fakturoid Local(SilentServer server)
        {
            // Own HttpClient: a pooled connection of another test never serves this one.
            return new Birko.Fakturoid.Fakturoid(new Birko.Fakturoid.Authentication.ClientCredentialsTokenProvider("id", "secret"),
                "applecorp", "FakturoidTests", "tests@example.com", new HttpClient(), server.Url);
        }

        [Fact]
        public async Task CancelledTokenAbortsSlowRequest()
        {
            using var server = new SilentServer();
            var client = Local(server);
            client.Timeout = TimeSpan.FromMinutes(1);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            var stopwatch = Stopwatch.StartNew();

            var ex = await Should.ThrowAsync<OperationCanceledException>(() => Bounded(client.Invoices.View(1, cts.Token)));

            ex.CancellationToken.ShouldBe(cts.Token);
            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
            var connection = await server.SilentConnection.WaitAsync(TimeSpan.FromSeconds(5));
            (await SilentServer.ClosedByClient(connection, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        }

        [Fact]
        public async Task CancellationIsNotWrappedAsAFakturoidException()
        {
            var handler = new StubHandler().Enqueue(async (request, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException("unreachable");
            });
            var client = TestClient.Create(handler);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            var ex = await Should.ThrowAsync<Exception>(() => Bounded(client.Subjects.Add(new Birko.Fakturoid.Request.Subject { Name = "x" }, cts.Token)));

            ex.ShouldBeAssignableTo<OperationCanceledException>();
            ex.ShouldNotBeAssignableTo<FakturoidException>();
        }

        [Fact]
        public async Task CancellationWhileReadingTheBody()
        {
            // Headers arrive, the body never does: the token must reach the content read.
            var handler = new StubHandler().Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new EndlessStream()) });
            var client = TestClient.Create(handler);
            client.Timeout = TimeSpan.FromMinutes(1);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            var stopwatch = Stopwatch.StartNew();

            await Should.ThrowAsync<OperationCanceledException>(() => Bounded(client.Invoices.DownloadPdf(1, cts.Token)));

            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task CancellationWhileFetchingTheAccessToken()
        {
            var handler = new StubHandler { TokenDelay = TimeSpan.FromMinutes(1) };
            var client = TestClient.Create(handler);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            await Should.ThrowAsync<OperationCanceledException>(() => Bounded(client.Account.View(cts.Token)));

            handler.ApiRequests.ShouldBeEmpty();
        }

        [Fact]
        public async Task AlreadyCancelledTokenSendsNothing()
        {
            var handler = new StubHandler();
            var client = TestClient.Create(handler);
            var events = 0;
            client.RequestCompleted += (s, e) => events++;

            await Should.ThrowAsync<OperationCanceledException>(() => Bounded(client.Users.Current(new CancellationToken(true))));

            handler.Requests.ShouldBeEmpty();
            events.ShouldBe(0);
        }

        [Fact]
        public async Task CancelledRequestRaisesRequestCompleted()
        {
            var handler = new StubHandler().Enqueue(async (request, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            var client = TestClient.Create(handler);
            var events = new List<Birko.Fakturoid.RequestCompletedEventArgs>();
            client.RequestCompleted += (s, e) => events.Add(e);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            await Should.ThrowAsync<OperationCanceledException>(() => Bounded(client.Invoices.Lock(7, cts.Token)));

            var cancelled = events.Last();
            cancelled.Method.ShouldBe("POST");
            cancelled.Uri.ShouldBe("accounts/applecorp/invoices/7/fire.json?event=lock");
            cancelled.StatusCode.ShouldBeNull();
            cancelled.Exception.ShouldBeAssignableTo<OperationCanceledException>();
        }

        [Fact]
        public async Task OwnTimeoutIsATimeoutException()
        {
            var handler = new StubHandler().Enqueue(async (request, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            var client = TestClient.Create(handler);
            client.Timeout = TimeSpan.FromMilliseconds(200);

            var ex = await Should.ThrowAsync<TimeoutException>(() => Bounded(client.Account.View()));

            ex.InnerException.ShouldBeAssignableTo<OperationCanceledException>();
        }

        [Fact]
        public async Task EveryPublicAsyncMethodTakesACancellationToken()
        {
            var types = typeof(Birko.Fakturoid.Fakturoid).Assembly.GetTypes()
                .Where(t => t.IsPublic && t.Namespace == "Birko.Fakturoid");
            var missing = types
                .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
                .Where(m => typeof(Task).IsAssignableFrom(m.ReturnType))
                .Where(m =>
                {
                    var last = m.GetParameters().LastOrDefault();
                    return last == null || last.ParameterType != typeof(CancellationToken) || !last.HasDefaultValue;
                })
                .Select(m => m.DeclaringType!.Name + "." + m.Name)
                .ToList();

            missing.ShouldBeEmpty();
            await Task.CompletedTask;
        }
    }
}
