using Birko.Fakturoid.Json;
using Birko.Fakturoid.Response;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;
using Req = Birko.Fakturoid.Request;

namespace Fakturoid.Tests
{
    // Offline: requests and responses against the JSON examples of the API documentation (TestData).
    public class SerializationTest
    {
        private static T Read<T>(string file) => FakturoidJson.Deserialize<T>(TestClient.Data(file));

        [Fact]
        public void InvoiceIndexExampleReadsEveryAttribute()
        {
            var invoices = Read<List<Invoice>>("invoice-index.json");

            var invoice = invoices.ShouldHaveSingleItem();
            invoice.Id.ShouldBe(27);
            invoice.DocumentType.ShouldBe("invoice");
            invoice.Status.ShouldNotBeNullOrEmpty();
            invoice.IssuedOn.ShouldNotBeNull();
            invoice.Total.ShouldNotBeNull();
            invoice.Lines.ShouldNotBeEmpty();
            invoice.Lines[0].UnitPrice.ShouldNotBeNull();
            invoice.Lines[0].UnitPriceWithVat.ShouldNotBeNull();
            invoice.VatRatesSummary.ShouldNotBeEmpty();
            invoice.Lines.Where(l => l.Inventory != null).ShouldNotBeEmpty();
            // Every documented attribute is modelled.
            (invoice.ExtensionData ?? new Dictionary<string, JsonElement>()).Keys.ShouldBeEmpty();
            invoice.Lines.ShouldAllBe(l => l.ExtensionData == null || l.ExtensionData.Count == 0);
        }

        [Fact]
        public void DecimalsSentAsStringsAreRead()
        {
            var line = FakturoidJson.Deserialize<InvoiceLine>("{\"id\":1304,\"name\":\"Disk 2TB\",\"quantity\":\"2.0\",\"unit_price\":\"1000.0\",\"vat_rate\":21,\"unit_price_with_vat\":\"1210.0\"}");

            line.Quantity.ShouldBe(2m);
            line.UnitPrice.ShouldBe(1000m);
            line.VatRate.ShouldBe(21m);
            line.UnitPriceWithVat.ShouldBe(1210m);
        }

        [Fact]
        public void InvoiceCreateRequestMatchesTheDocumentation()
        {
            var invoice = new Req.Invoice
            {
                CustomId = "1234",
                SubjectId = 16,
                Due = 21,
                IssuedOn = new DateTime(2023, 11, 19),
                TaxableFulfillmentDue = new DateTime(2023, 11, 19),
                Lines = new List<Req.InvoiceLine>
                {
                    new Req.InvoiceLine { Name = "Hard work", Quantity = 1.0m, UnitName = "h", UnitPrice = 40000m, VatRate = 21m },
                },
            };

            FakturoidJson.Serialize(invoice).ShouldBe(
                "{\"custom_id\":\"1234\",\"subject_id\":16,\"issued_on\":\"2023-11-19\",\"taxable_fulfillment_due\":\"2023-11-19\",\"due\":21," +
                "\"lines\":[{\"name\":\"Hard work\",\"quantity\":1.0,\"unit_name\":\"h\",\"unit_price\":40000,\"vat_rate\":21}]}");
        }

        [Fact]
        public void NullPropertiesAreNotSent()
        {
            FakturoidJson.Serialize(new Req.Subject { City = "Praha" }).ShouldBe("{\"city\":\"Praha\"}");
            FakturoidJson.Serialize(new Req.InvoicePayment()).ShouldBe("{}");
        }

        [Fact]
        public void LineDeletionSendsDestroy()
        {
            var edit = new Req.Invoice { Lines = new List<Req.InvoiceLine> { new Req.InvoiceLine { Id = 1234, Destroy = true } } };

            FakturoidJson.Serialize(edit).ShouldBe("{\"lines\":[{\"id\":1234,\"_destroy\":true}]}");
        }

        [Fact]
        public void ResponseSentAsRequestSendsOnlyWritableAttributes()
        {
            var subject = Read<List<Subject>>("subject-index.json")[0];

            var json = FakturoidJson.Serialize<Req.Subject>(subject);

            json.ShouldContain("\"name\":\"Apple Czech s.r.o.\"");
            json.ShouldNotContain("\"id\"");
            json.ShouldNotContain("html_url");
            json.ShouldNotContain("created_at");
        }

        [Fact]
        public void CzechTextStaysReadable()
        {
            FakturoidJson.Serialize(new Req.Subject { City = "Plzeň" }).ShouldBe("{\"city\":\"Plzeň\"}");
        }

        [Fact]
        public void AttachmentFromBytesIsADataUri()
        {
            var attachment = Req.Attachment.FromBytes("a.pdf", "application/pdf", new byte[] { 1, 2, 3 });

            attachment.DataUrl.ShouldBe("data:application/pdf;base64,AQID");
            FakturoidJson.Serialize(attachment).ShouldBe("{\"filename\":\"a.pdf\",\"data_url\":\"data:application/pdf;base64,AQID\"}");
        }

        [Fact]
        public void SubjectIndexExampleReadsEveryAttribute()
        {
            var subject = Read<List<Subject>>("subject-index.json").ShouldHaveSingleItem();

            subject.Id.ShouldBe(16);
            subject.Name.ShouldBe("Apple Czech s.r.o.");
            subject.RegistrationNo.ShouldBe("28897501");
            subject.SettingUpdateFromAres.ShouldBe("inherit");
            subject.ClientPortalUrl.ShouldNotBeNull();
            subject.CreatedAt.ShouldBe(new DateTimeOffset(2023, 8, 22, 10, 59, 0, 330, TimeSpan.FromHours(2)));
            (subject.ExtensionData ?? new Dictionary<string, JsonElement>()).Keys.ShouldBeEmpty();
        }

        [Fact]
        public void CurrentUserExampleListsAccounts()
        {
            var user = Read<User>("user-current.json");

            user.Id.ShouldBe(5);
            user.DefaultAccount.ShouldBeNull();
            var account = user.Accounts.ShouldHaveSingleItem();
            account.Slug.ShouldBe("applecorp");
            account.AllowedScope.ShouldBe(new[] { "invoices", "expenses", "reports" });
        }

        [Fact]
        public void AccountExampleReadsEveryAttribute()
        {
            var account = Read<Account>("account.json");

            account.Subdomain.ShouldBe("applecorp");
            account.ApiCallsLimit.ShouldBe(20000);
            account.VatRate.ShouldBe(21m);
            (account.ExtensionData ?? new Dictionary<string, JsonElement>()).Keys.ShouldBeEmpty();
        }

        [Fact]
        public void PaymentRoundTrip()
        {
            FakturoidJson.Serialize(new Req.InvoicePayment { PaidOn = new DateTime(2023, 12, 6) }).ShouldBe("{\"paid_on\":\"2023-12-06\"}");

            var payment = Read<InvoicePayment>("payment-created.json");
            payment.Id.ShouldBe(2);
            payment.PaidOn.ShouldBe(new DateTime(2023, 12, 8));
            payment.Amount.ShouldBe(1210m);
            payment.BankAccountId.ShouldBe(11);
            payment.TaxDocumentId.ShouldBeNull();
            (payment.ExtensionData ?? new Dictionary<string, JsonElement>()).Keys.ShouldBeEmpty();
        }

        [Fact]
        public void WebhookRoundTrip()
        {
            var request = new Req.Webhook
            {
                WebhookUrl = "https://example.com/webhook",
                AuthHeader = "Bearer TOKEN",
                Events = new List<string> { Birko.Fakturoid.ValueLists.WebhookEvent.InvoiceCreated },
            };
            FakturoidJson.Serialize(request).ShouldBe("{\"webhook_url\":\"https://example.com/webhook\",\"auth_header\":\"Bearer TOKEN\",\"events\":[\"invoice_created\"]}");

            var webhook = Read<Webhook>("webhook.json");
            webhook.Id.ShouldBe(5);
            webhook.FailedDeliveriesUuid.ShouldBe("b3518e74-5c9f-4b26-a5e2-6a8021c94a77");
            webhook.Active.ShouldBe(true);
            webhook.Events.Count.ShouldBe(3);
            (webhook.ExtensionData ?? new Dictionary<string, JsonElement>()).Keys.ShouldBeEmpty();
        }

        [Fact]
        public void FailedDeliveriesExample()
        {
            var failed = Read<List<FailedWebhookDelivery>>("webhook-failed-deliveries.json").ShouldHaveSingleItem();

            failed.EventName.ShouldBe("invoice_created");
            failed.IdempotencyKey.ShouldBe("019505e0-85b3-7a4b-a163-740753e8e21a");
            failed.Deliveries.Count.ShouldBe(3);
            failed.Deliveries[0].ResponseStatus.ShouldBe("ssl_error");
            failed.Body.GetProperty("event_name").GetString().ShouldBe("invoice_created");
        }

        [Fact]
        public void ResponseStatusMayBeANumber()
        {
            FakturoidJson.Deserialize<WebhookDeliveryAttempt>("{\"id\":1,\"response_status\":404}").ResponseStatus.ShouldBe("404");
        }

        [Fact]
        public void DatesWithoutValueAreNull()
        {
            var invoice = FakturoidJson.Deserialize<Invoice>("{\"id\":1,\"due_on\":null,\"paid_on\":\"2024-06-13\"}");

            invoice.DueOn.ShouldBeNull();
            invoice.PaidOn.ShouldBe(new DateTime(2024, 6, 13));
        }

        [Fact]
        public void UnknownAttributesAreKept()
        {
            var subject = FakturoidJson.Deserialize<Subject>("{\"id\":1,\"name\":\"A\",\"brand_new\":42}");

            subject.ExtensionData["brand_new"].GetInt32().ShouldBe(42);
        }
    }
}
