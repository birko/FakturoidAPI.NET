using Birko.Fakturoid;
using Birko.Fakturoid.Exceptions;
using Shouldly;
using System;
using System.Text;
using Xunit;

namespace Fakturoid.Tests
{
    // Offline: verifying and reading a webhook delivery.
    public class WebhookReceiverTest
    {
        [Fact]
        public void AuthorizationMustMatchTheWebhookAuthHeader()
        {
            WebhookReceiver.VerifyAuthorization("Bearer TOKEN", "Bearer TOKEN").ShouldBeTrue();
            WebhookReceiver.VerifyAuthorization("Bearer TOKEN2", "Bearer TOKEN").ShouldBeFalse();
            WebhookReceiver.VerifyAuthorization("Bearer TOKE", "Bearer TOKEN").ShouldBeFalse();
            WebhookReceiver.VerifyAuthorization("bearer token", "Bearer TOKEN").ShouldBeFalse();
            WebhookReceiver.VerifyAuthorization(null, "Bearer TOKEN").ShouldBeFalse();
            WebhookReceiver.VerifyAuthorization("", "Bearer TOKEN").ShouldBeFalse();
        }

        [Fact]
        public void WithoutAnExpectedValueNothingIsVerified()
        {
            Should.Throw<ArgumentException>(() => WebhookReceiver.VerifyAuthorization("anything", null));
            Should.Throw<ArgumentException>(() => WebhookReceiver.VerifyAuthorization("", ""));
        }

        [Fact]
        public void InvoicePaidPayload()
        {
            var body = TestClient.Data("webhook-payload-invoice-paid.json");

            var payload = WebhookReceiver.Parse(Encoding.UTF8.GetBytes(body));

            payload.WebhookId.ShouldBe(5);
            payload.EventName.ShouldBe(Birko.Fakturoid.ValueLists.WebhookEvent.InvoicePaid);
            payload.CreatedAt.ShouldBe(new DateTimeOffset(2024, 6, 13, 14, 6, 20, 924, TimeSpan.FromHours(2)));
            payload.Invoice.Id.ShouldBe(93);
            payload.Invoice.Lines[0].Name.ShouldBe("Magic mouse");
            payload.Invoice.Payments[0].Amount.ShouldBe(12100m);
            payload.Payment.Id.ShouldBe(785);
            payload.Subject.ShouldBeNull();
        }

        [Fact]
        public void BulkPayload()
        {
            var payload = WebhookReceiver.Parse("{\"webhook_id\":13,\"event_name\":\"inbox_file_removed_all\",\"created_at\":\"2024-06-13T14:16:33.114+02:00\",\"body\":{\"inbox_files\":{\"count\":2}}}");

            payload.Body.GetProperty("inbox_files").GetProperty("count").GetInt32().ShouldBe(2);
            payload.Invoice.ShouldBeNull();
        }

        [Fact]
        public void NotAWebhook()
        {
            Should.Throw<ParseException>(() => WebhookReceiver.Parse("<html/>"));
            Should.Throw<ParseException>(() => WebhookReceiver.Parse("{}"));
            Should.Throw<ParseException>(() => WebhookReceiver.Parse(""));
        }
    }
}
