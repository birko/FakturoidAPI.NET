using Birko.Fakturoid;
using Birko.Fakturoid.Exceptions;
using Shouldly;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Req = Birko.Fakturoid.Request;

namespace Fakturoid.Tests
{
    // Offline: HTTP errors -> typed exceptions, rate limit headers (stubbed HttpMessageHandler).
    public class ErrorsTest
    {
        [Fact]
        public async Task ValidationErrorsCarryTheFieldMap()
        {
            var handler = new StubHandler().EnqueueJson((HttpStatusCode)422,
                "{\"errors\":{\"number\":[\"The number does not match the number format in the settings\"],\"subject_id\":[\"can't be blank\",\"Contact does not exist.\"]}}");
            var client = TestClient.Create(handler);

            var ex = await Should.ThrowAsync<ValidationException>(() => client.Invoices.Add(new Req.Invoice()));

            ex.StatusCode.ShouldBe(422);
            ex.Errors["subject_id"].ShouldBe(new[] { "can't be blank", "Contact does not exist." });
            ex.Errors.Keys.ShouldContain("number");
            ex.Message.ShouldContain("subject_id: can't be blank, Contact does not exist.");
            ex.Method.ShouldBe("POST");
            ex.Uri.ShouldBe("accounts/applecorp/invoices.json");
        }

        [Fact]
        public async Task MissingBankAccountOnInvoiceCreate()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.Forbidden,
                "{\"errors\":{\"bank_account\":[\"Please set up a bank account in your Fakturoid account https://app.fakturoid.cz/applecorp/settings/bank_accounts to create an invoice.\"]}}");
            var client = TestClient.Create(handler);

            var ex = await Should.ThrowAsync<MissingBankAccountException>(() => client.Invoices.Add(new Req.Invoice { SubjectId = 1 }));

            ex.ShouldBeAssignableTo<ForbiddenException>();
            ex.Errors["bank_account"][0].ShouldContain("settings/bank_accounts");
        }

        [Fact]
        public async Task LockedDocument()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.Forbidden,
                "{\"error\":\"document_locked\",\"error_description\":\"Document first needs to be unlocked to perform the action\"}");
            var client = TestClient.Create(handler);

            var ex = await Should.ThrowAsync<DocumentLockedException>(() => client.Invoices.Edit(1, new Req.Invoice { Note = "x" }));

            ex.Error.ShouldBe("document_locked");
            ex.ErrorDescription.ShouldBe("Document first needs to be unlocked to perform the action");
            ex.Errors.ShouldBeEmpty();
        }

        [Fact]
        public async Task OtherForbidden()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.Forbidden,
                "{\"error\":\"forbidden\",\"error_description\":\"Payment cannot be added to already paid invoice\"}");
            var client = TestClient.Create(handler);

            var ex = await Should.ThrowAsync<ForbiddenException>(() => client.InvoicePayments.Add(1));

            ex.GetType().ShouldBe(typeof(ForbiddenException));
            ex.Message.ShouldContain("already paid");
        }

        [Fact]
        public async Task BlockedAccount()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.PaymentRequired, "[{\"number\":\"2024-0001\",\"total\":\"300.0\"}]");
            var client = TestClient.Create(handler);

            var ex = await Should.ThrowAsync<AccountBlockedException>(() => client.Subjects.View(1));

            ex.StatusCode.ShouldBe(402);
            ex.ResponseBody.ShouldContain("2024-0001");
        }

        [Fact]
        public async Task NotFound()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.NotFound, "{\"error\":\"not_found\",\"error_description\":\"Record not found\"}");
            var client = TestClient.Create(handler);

            await Should.ThrowAsync<NotFoundException>(() => client.Invoices.View(999));
        }

        [Fact]
        public async Task BadRequestKeepsCodeAndDescription()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.BadRequest,
                "{\"error\":\"invalid_request\",\"error_description\":\"Required header \\\"User-Agent\\\" is missing\"}");
            var client = TestClient.Create(handler);

            var ex = await Should.ThrowAsync<ApiException>(() => client.Account.View());

            ex.GetType().ShouldBe(typeof(ApiException));
            ex.Error.ShouldBe("invalid_request");
            ex.ErrorDescription.ShouldContain("User-Agent");
        }

        [Fact]
        public async Task NonJsonErrorIsStillAnApiException()
        {
            var handler = new StubHandler().Enqueue(new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("<html>Bad gateway</html>", Encoding.UTF8, "text/html"),
            });
            var client = TestClient.Create(handler);

            var ex = await Should.ThrowAsync<ApiException>(() => client.Account.View());

            ex.StatusCode.ShouldBe(502);
            ex.Error.ShouldBeNull();
        }

        [Fact]
        public async Task Maintenance()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.ServiceUnavailable, "{\"error\":\"read_only\",\"error_description\":\"Maintenance\"}");
            var client = TestClient.Create(handler);

            await Should.ThrowAsync<ServiceUnavailableException>(() => client.Subjects.List());
        }

        [Fact]
        public async Task UnparseableSuccessIsAParseException()
        {
            var handler = new StubHandler().Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>maintenance</html>", Encoding.UTF8, "text/html"),
            });
            var client = TestClient.Create(handler);

            var ex = await Should.ThrowAsync<ParseException>(() => client.Account.View());

            ex.ShouldNotBeAssignableTo<ApiException>();
            ex.StatusCode.ShouldBe(200);
            ex.ResponseExcerpt.ShouldContain("maintenance");
        }

        [Fact]
        public async Task RateLimitHeadersAreExposed()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.OK, TestClient.Data("account.json"),
                ("X-RateLimit-Policy", "default;q=400;w=60"), ("X-RateLimit", "default;r=398;t=55"));
            var client = TestClient.Create(handler);
            client.RateLimit.ShouldBeNull();
            var before = DateTimeOffset.UtcNow;

            await client.Account.View();

            var limit = client.RateLimit.ShouldNotBeNull();
            limit.Policy.ShouldBe("default");
            limit.Quota.ShouldBe(400);
            limit.Window.ShouldBe(TimeSpan.FromSeconds(60));
            limit.Remaining.ShouldBe(398);
            limit.ResetIn.ShouldBe(TimeSpan.FromSeconds(55));
            limit.ResetAt!.Value.ShouldBeInRange(before.AddSeconds(55), DateTimeOffset.UtcNow.AddSeconds(55));
        }

        [Fact]
        public void SeveralPoliciesReportTheClosestToItsLimit()
        {
            var parsed = RateLimit.Parse(new[] { "default;r=398;t=55, burst;r=3;t=1" });

            parsed.Count.ShouldBe(2);
            parsed["burst"]["r"].ShouldBe("3");
        }

        [Fact]
        public async Task TooManyRequestsCarriesTheResetTimeAndDoesNotWait()
        {
            var handler = new StubHandler().EnqueueJson((HttpStatusCode)429, "{\"error\":\"too_many_requests\",\"error_description\":\"Rate limit exceeded\"}",
                ("X-RateLimit-Policy", "default;q=400;w=60"), ("X-RateLimit", "default;r=0;t=42"));
            var client = TestClient.Create(handler);
            var before = DateTimeOffset.UtcNow;

            var ex = await Should.ThrowAsync<RateLimitExceededException>(() => client.Invoices.List());

            ex.ResetAt!.Value.ShouldBeInRange(before.AddSeconds(42), DateTimeOffset.UtcNow.AddSeconds(42));
            ex.RateLimit!.Remaining.ShouldBe(0);
            (DateTimeOffset.UtcNow - before).ShouldBeLessThan(TimeSpan.FromSeconds(5));
            handler.ApiRequests.ShouldHaveSingleItem();
        }

        [Fact]
        public async Task TooManyRequestsFallsBackToRetryAfter()
        {
            var handler = new StubHandler().EnqueueJson((HttpStatusCode)429, "{}", ("Retry-After", "30"));
            var client = TestClient.Create(handler);
            var before = DateTimeOffset.UtcNow;

            var ex = await Should.ThrowAsync<RateLimitExceededException>(() => client.Invoices.List());

            ex.ResetAt!.Value.ShouldBeInRange(before.AddSeconds(30), DateTimeOffset.UtcNow.AddSeconds(30));
        }
    }
}
