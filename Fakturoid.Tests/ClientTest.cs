using Birko.Fakturoid.Exceptions;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Req = Birko.Fakturoid.Request;

namespace Fakturoid.Tests
{
    // Offline: URLs, headers, paging and the endpoints of phase 1 against a stubbed HttpMessageHandler.
    public class ClientTest
    {
        private static string Item(int id) => $"{{\"id\":{id},\"name\":\"S{id}\"}}";

        private static string Items(int from, int count) => "[" + string.Join(",", Enumerable.Range(from, count).Select(Item)) + "]";

        [Fact]
        public void UserAgentNeedsNameAndEmail()
        {
            Should.Throw<ArgumentException>(() => new Birko.Fakturoid.Fakturoid("id", "secret", "slug", "", "a@b.cz"));
            Should.Throw<ArgumentException>(() => new Birko.Fakturoid.Fakturoid("id", "secret", "slug", "App", "not-an-email"));
            Should.Throw<ArgumentException>(() => new Birko.Fakturoid.Fakturoid("", "secret", "slug", "App", "a@b.cz"));
            new Birko.Fakturoid.Fakturoid("id", "secret", "slug", "Symbio", "dev@example.com").UserAgent.ShouldBe("Symbio (dev@example.com)");
        }

        [Fact]
        public async Task EveryRequestSendsUserAgentAndJson()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.OK, TestClient.Data("user-current.json"));
            var client = TestClient.Create(handler);

            var user = await client.Users.Current();

            user.Accounts[0].Slug.ShouldBe("applecorp");
            handler.Requests.Count.ShouldBe(2);
            handler.Requests.ShouldAllBe(r => r.UserAgent == "FakturoidTests (tests@example.com)");
            var api = handler.ApiRequests.Single();
            api.Uri.ToString().ShouldBe("https://app.fakturoid.cz/api/v3/user.json");
            api.Accept.ShouldBe("application/json");
            api.Authorization.ShouldBe("Bearer token-1");
        }

        [Fact]
        public async Task AccountEndpointsNeedTheSlug()
        {
            var client = TestClient.Create(new StubHandler(), slug: null);

            await Should.ThrowAsync<InvalidOperationException>(() => client.Account.View());
        }

        [Fact]
        public async Task SubjectCreateSendsJson()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.Created, "{\"id\":41,\"name\":\"MICROSOFT s.r.o.\",\"user_id\":5}",
                ("Location", "https://app.fakturoid.cz/api/v3/accounts/applecorp/subjects/41.json"));
            var client = TestClient.Create(handler);

            var subject = await client.Subjects.Add(new Req.Subject { Name = "MICROSOFT s.r.o.", RegistrationNo = "47123737" });

            subject.Id.ShouldBe(41);
            var request = handler.ApiRequests.Single();
            request.Method.ShouldBe(HttpMethod.Post);
            request.PathAndQuery.ShouldBe("/api/v3/accounts/applecorp/subjects.json");
            request.ContentType.ShouldBe("application/json; charset=utf-8");
            request.Body.ShouldBe("{\"name\":\"MICROSOFT s.r.o.\",\"registration_no\":\"47123737\"}");
        }

        [Fact]
        public async Task SubjectEditIsAPatch()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.OK, "{\"id\":41,\"name\":\"M\",\"city\":\"Praha\"}");
            var client = TestClient.Create(handler);

            await client.Subjects.Edit(41, new Req.Subject { City = "Praha" });

            var request = handler.ApiRequests.Single();
            request.Method.Method.ShouldBe("PATCH");
            request.PathAndQuery.ShouldBe("/api/v3/accounts/applecorp/subjects/41.json");
            request.Body.ShouldBe("{\"city\":\"Praha\"}");
        }

        [Fact]
        public async Task SubjectFilterAndSearch()
        {
            var handler = new StubHandler()
                .EnqueueJson(HttpStatusCode.OK, "[" + Item(7) + "]")
                .EnqueueJson(HttpStatusCode.OK, "[]")
                .EnqueueJson(HttpStatusCode.OK, "[]");
            var client = TestClient.Create(handler);

            var found = await client.Subjects.FindByCustomId("symbio/315");
            var missing = await client.Subjects.FindByCustomId("nope");
            await client.Subjects.Search("Apple s.r.o.", page: 2);

            found!.Id.ShouldBe(7);
            missing.ShouldBeNull();
            var paths = handler.ApiRequests.Select(r => r.Uri.PathAndQuery).ToList();
            paths[0].ShouldBe("/api/v3/accounts/applecorp/subjects.json?custom_id=symbio%2F315");
            paths[2].ShouldBe("/api/v3/accounts/applecorp/subjects/search.json?query=Apple%20s.r.o.&page=2");
        }

        [Fact]
        public async Task InvoiceFilterEncodesEveryParameter()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.OK, "[]");
            var client = TestClient.Create(handler);

            await client.Invoices.List(new Req.InvoiceFilter
            {
                Since = new DateTimeOffset(2023, 8, 25, 10, 55, 14, TimeSpan.FromHours(2)),
                UpdatedUntil = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero),
                SubjectId = 5,
                CustomId = "315",
                Number = "2023-0005",
                Status = Birko.Fakturoid.ValueLists.InvoiceStatus.Paid,
                DocumentType = Birko.Fakturoid.ValueLists.DocumentTypeFilter.Regular,
            }, page: 3);

            handler.ApiRequests.Single().Uri.Query.ShouldBe(
                "?since=2023-08-25T10%3A55%3A14%2B02%3A00&updated_until=2024-01-02T03%3A04%3A05%2B00%3A00" +
                "&subject_id=5&custom_id=315&number=2023-0005&status=paid&document_type=regular&page=3");
        }

        [Fact]
        public async Task InvoiceSearchWithTags()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.OK, "[]");
            var client = TestClient.Create(handler);

            await client.Invoices.Search("hosting", new[] { "PPC", "web" });

            handler.ApiRequests.Single().Uri.PathAndQuery.ShouldBe("/api/v3/accounts/applecorp/invoices/search.json?query=hosting&tags%5B%5D=PPC&tags%5B%5D=web");
        }

        [Fact]
        public async Task PageReportsWhetherMoreMayFollow()
        {
            var handler = new StubHandler()
                .EnqueueJson(HttpStatusCode.OK, Items(1, 40))
                .EnqueueJson(HttpStatusCode.OK, Items(41, 3));
            var client = TestClient.Create(handler);

            var first = await client.Subjects.List();
            var second = await client.Subjects.List(page: 2);

            first.Number.ShouldBe(1);
            first.MayHaveMore.ShouldBeTrue();
            second.MayHaveMore.ShouldBeFalse();
            await Should.ThrowAsync<ArgumentOutOfRangeException>(() => client.Subjects.List(page: 0));
        }

        [Fact]
        public async Task ListAllReadsUntilAnEmptyPage()
        {
            var handler = new StubHandler()
                .EnqueueJson(HttpStatusCode.OK, Items(1, 40))
                .EnqueueJson(HttpStatusCode.OK, Items(41, 40))
                .EnqueueJson(HttpStatusCode.OK, Items(81, 5))
                .EnqueueJson(HttpStatusCode.OK, "[]");
            var client = TestClient.Create(handler);

            var all = await client.Subjects.ListAll(new Req.SubjectFilter { UpdatedSince = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero) });

            all.Count.ShouldBe(85);
            all.Select(s => s.Id).ShouldBe(Enumerable.Range(1, 85));
            handler.ApiRequests.Select(r => r.Uri.Query).ShouldBe(new[]
            {
                "?updated_since=2024-01-01T00%3A00%3A00%2B00%3A00",
                "?updated_since=2024-01-01T00%3A00%3A00%2B00%3A00&page=2",
                "?updated_since=2024-01-01T00%3A00%3A00%2B00%3A00&page=3",
                "?updated_since=2024-01-01T00%3A00%3A00%2B00%3A00&page=4",
            });
        }

        [Fact]
        public async Task ListAllStopsAtMaxPages()
        {
            var handler = new StubHandler()
                .EnqueueJson(HttpStatusCode.OK, Items(1, 40))
                .EnqueueJson(HttpStatusCode.OK, Items(41, 40));
            var client = TestClient.Create(handler);

            (await client.Invoices.ListAll(maxPages: 2)).Count.ShouldBe(80);
            handler.ApiRequests.Count().ShouldBe(2);
        }

        [Fact]
        public async Task InvoiceLifecycleEndpoints()
        {
            var handler = new StubHandler()
                .EnqueueJson(HttpStatusCode.Created, TestClient.Data("invoice-created.json"), ("Location", "https://app.fakturoid.cz/api/v3/accounts/applecorp/invoices/529.json"))
                .EnqueueJson(HttpStatusCode.OK, TestClient.Data("invoice-created.json"))
                .EnqueueStatus(HttpStatusCode.NoContent)
                .EnqueueStatus(HttpStatusCode.NoContent)
                .EnqueueStatus(HttpStatusCode.NoContent)
                .EnqueueStatus(HttpStatusCode.NoContent);
            var client = TestClient.Create(handler);

            var created = await client.Invoices.Add(new Req.Invoice { CustomId = "1234", SubjectId = 16 });
            await client.Invoices.View(created.Id);
            await client.Invoices.Lock(created.Id);
            await client.Invoices.MarkAsUncollectible(created.Id);
            await client.Invoices.Cancel(created.Id);
            await client.Invoices.Delete(created.Id);

            created.Id.ShouldBe(529);
            created.DueOn.ShouldBe(new DateTime(2023, 12, 10));
            handler.ApiRequests.Select(r => $"{r.Method} {r.Uri.PathAndQuery}").ShouldBe(new[]
            {
                "POST /api/v3/accounts/applecorp/invoices.json",
                "GET /api/v3/accounts/applecorp/invoices/529.json",
                "POST /api/v3/accounts/applecorp/invoices/529/fire.json?event=lock",
                "POST /api/v3/accounts/applecorp/invoices/529/fire.json?event=mark_as_uncollectible",
                "POST /api/v3/accounts/applecorp/invoices/529/fire.json?event=cancel",
                "DELETE /api/v3/accounts/applecorp/invoices/529.json",
            });
            handler.ApiRequests.Skip(2).ShouldAllBe(r => r.Body == null);
        }

        [Fact]
        public async Task ActionThatCannotBeFired()
        {
            var handler = new StubHandler().EnqueueJson((HttpStatusCode)422, "{\"errors\":{\"event\":[\"lock cannot be fired\"]}}");
            var client = TestClient.Create(handler);

            var ex = await Should.ThrowAsync<ValidationException>(() => client.Invoices.Lock(1));

            ex.Errors["event"].ShouldBe(new[] { "lock cannot be fired" });
        }

        [Fact]
        public async Task FindInvoicesByCustomIdReadsAllPages()
        {
            var handler = new StubHandler()
                .EnqueueJson(HttpStatusCode.OK, "[{\"id\":1,\"custom_id\":\"A-1\"}]")
                .EnqueueJson(HttpStatusCode.OK, "[]");
            var client = TestClient.Create(handler);

            var found = await client.Invoices.FindByCustomId("A-1");

            found.ShouldHaveSingleItem().CustomId.ShouldBe("A-1");
            handler.ApiRequests.First().Uri.Query.ShouldBe("?custom_id=A-1");
        }

        [Fact]
        public async Task PaymentEndpoints()
        {
            var handler = new StubHandler()
                .EnqueueJson(HttpStatusCode.Created, TestClient.Data("payment-created.json"))
                .EnqueueJson(HttpStatusCode.Created, TestClient.Data("payment-created.json"))
                .EnqueueStatus(HttpStatusCode.NoContent);
            var client = TestClient.Create(handler);

            var payment = await client.InvoicePayments.Add(9, new Req.InvoicePayment { PaidOn = new DateTime(2023, 12, 6) });
            await client.InvoicePayments.CreateTaxDocument(9, payment.Id);
            await client.InvoicePayments.Delete(9, payment.Id);

            handler.ApiRequests.Select(r => $"{r.Method} {r.Uri.PathAndQuery} {r.Body}").ShouldBe(new[]
            {
                "POST /api/v3/accounts/applecorp/invoices/9/payments.json {\"paid_on\":\"2023-12-06\"}",
                "POST /api/v3/accounts/applecorp/invoices/9/payments/2/create_tax_document.json ",
                "DELETE /api/v3/accounts/applecorp/invoices/9/payments/2.json ",
            });
        }

        [Fact]
        public async Task WebhookEndpoints()
        {
            var webhook = TestClient.Data("webhook.json");
            var handler = new StubHandler()
                .EnqueueJson(HttpStatusCode.Created, webhook)
                .EnqueueJson(HttpStatusCode.OK, "[" + webhook + "]")
                .EnqueueJson(HttpStatusCode.OK, webhook)
                .EnqueueJson(HttpStatusCode.OK, TestClient.Data("webhook-failed-deliveries.json"))
                .EnqueueStatus(HttpStatusCode.NoContent);
            var client = TestClient.Create(handler);

            var created = await client.Webhooks.Add(new Req.Webhook { WebhookUrl = "https://example.com/webhook", AuthHeader = "Bearer TOKEN", Events = new List<string> { "invoice_paid" } });
            (await client.Webhooks.List()).Items.ShouldHaveSingleItem();
            await client.Webhooks.Edit(created.Id, new Req.Webhook { Active = false });
            (await client.Webhooks.FailedDeliveries(created.FailedDeliveriesUuid)).ShouldHaveSingleItem();
            await client.Webhooks.Delete(created.Id);

            handler.ApiRequests.Select(r => $"{r.Method} {r.Uri.PathAndQuery}").ShouldBe(new[]
            {
                "POST /api/v3/accounts/applecorp/webhooks.json",
                "GET /api/v3/accounts/applecorp/webhooks.json",
                "PATCH /api/v3/accounts/applecorp/webhooks/5.json",
                "GET /api/v3/accounts/applecorp/webhooks/b3518e74-5c9f-4b26-a5e2-6a8021c94a77/failed_deliveries.json",
                "DELETE /api/v3/accounts/applecorp/webhooks/5.json",
            });
        }
    }
}
