using Birko.Fakturoid;
using Birko.Fakturoid.Exceptions;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Req = Birko.Fakturoid.Request;

namespace Fakturoid.Tests
{
    // Offline: RequestCompleted reports every request and never exposes credentials.
    public class RequestCompletedTest
    {
        [Fact]
        public async Task BodiesOnlyWhenOptedIn()
        {
            var handler = new StubHandler()
                .EnqueueJson(HttpStatusCode.OK, "{\"id\":1,\"name\":\"A\"}")
                .EnqueueJson(HttpStatusCode.OK, "{\"id\":1,\"name\":\"A\"}");
            var client = TestClient.Create(handler);
            var events = new List<RequestCompletedEventArgs>();
            client.RequestCompleted += (s, e) => events.Add(e);

            await client.Subjects.Edit(1, new Req.Subject { Name = "A" });
            client.IncludeBodiesInEvents = true;
            await client.Subjects.Edit(1, new Req.Subject { Name = "A" });

            // token, PATCH, PATCH
            events.Count.ShouldBe(3);
            events[1].RequestBody.ShouldBeNull();
            events[1].ResponseBody.ShouldBeNull();
            events[1].ResponseLength.ShouldBe(19);
            events[2].Method.ShouldBe("PATCH");
            events[2].Uri.ShouldBe("accounts/applecorp/subjects/1.json");
            events[2].StatusCode.ShouldBe(200);
            events[2].RequestBody.ShouldBe("{\"name\":\"A\"}");
            events[2].ResponseBody.ShouldBe("{\"id\":1,\"name\":\"A\"}");
            events[2].Exception.ShouldBeNull();
        }

        [Fact]
        public async Task CredentialsAndSecretsNeverAppear()
        {
            var handler = new StubHandler()
                .EnqueueJson(HttpStatusCode.Created, "{\"id\":5,\"webhook_url\":\"https://example.com/hook\",\"auth_header\":\"Bearer HOOK-SECRET\",\"events\":[\"invoice_paid\"]}")
                .EnqueueJson(HttpStatusCode.OK, "{\"id\":9,\"token\":\"INVOICE-TOKEN\",\"public_html_url\":\"https://app.fakturoid.cz/applecorp/p/INVOICE-TOKEN/2024-0001\"}")
                .EnqueueJson((HttpStatusCode)422, "{\"errors\":{\"events\":[\"can't be empty\"]}}");
            var client = TestClient.Create(handler);
            client.IncludeBodiesInEvents = true;
            var events = new List<RequestCompletedEventArgs>();
            client.RequestCompleted += (s, e) => events.Add(e);

            await client.Webhooks.Add(new Req.Webhook { WebhookUrl = "https://example.com/hook", AuthHeader = "Bearer HOOK-SECRET", Events = new List<string> { "invoice_paid" } });
            await client.Invoices.View(9);
            await Should.ThrowAsync<ValidationException>(() => client.Webhooks.Edit(5, new Req.Webhook { AuthHeader = "Bearer HOOK-SECRET" }));

            var token = events.First();
            token.Uri.ShouldBe("oauth/token");
            token.RequestBody.ShouldBeNull();
            token.ResponseBody.ShouldBeNull();
            events[1].RequestBody.ShouldContain("\"auth_header\":\"***\"");
            events[2].ResponseBody.ShouldContain("\"token\":\"***\"");
            foreach (var e in events)
            {
                var text = $"{e.Method} {e.Uri} {e.RequestBody} {e.ResponseBody} {e.Exception}";
                text.ShouldNotContain(TestClient.ClientSecret);
                text.ShouldNotContain("token-1");
                text.ShouldNotContain("HOOK-SECRET");
                text.ShouldNotContain("INVOICE-TOKEN");
            }
        }

        [Fact]
        public async Task FailingHandlerDoesNotFailTheCall()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.Created, "{\"id\":41,\"name\":\"A\"}");
            var client = TestClient.Create(handler);
            var second = 0;
            client.RequestCompleted += (s, e) => throw new System.InvalidOperationException("logger down");
            client.RequestCompleted += (s, e) => second++;

            var subject = await client.Subjects.Add(new Req.Subject { Name = "A" });

            subject.Id.ShouldBe(41);
            second.ShouldBe(2);
        }

        [Fact]
        public async Task FailedRequestCarriesTheException()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.NotFound, "{\"error\":\"not_found\",\"error_description\":\"Record not found\"}");
            var client = TestClient.Create(handler);
            var events = new List<RequestCompletedEventArgs>();
            client.RequestCompleted += (s, e) => events.Add(e);

            var thrown = await Should.ThrowAsync<NotFoundException>(() => client.Subjects.View(3));

            events.Last().StatusCode.ShouldBe(404);
            events.Last().Exception.ShouldBeSameAs(thrown);
        }
    }
}
