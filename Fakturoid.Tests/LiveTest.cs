using Birko.Fakturoid.Exceptions;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Req = Birko.Fakturoid.Request;

namespace Fakturoid.Tests
{
    // Live credentials, read from a file outside the repository (never committed, never printed):
    //   ~/.fakturoid-test.env (or the path in FAKTUROID_TEST_ENV), lines KEY=VALUE:
    //   FAKTUROID_CLIENT_ID, FAKTUROID_CLIENT_SECRET, FAKTUROID_ACCOUNT_SLUG, FAKTUROID_CONTACT_EMAIL
    internal static class LiveSettings
    {
        private static readonly Lazy<IReadOnlyDictionary<string, string>> Values = new(Load);

        public static bool ReadEnabled => Environment.GetEnvironmentVariable("FAKTUROID_LIVE") == "1";

        // Creates and deletes documents: use a test account, never production.
        public static bool WriteEnabled => ReadEnabled && Environment.GetEnvironmentVariable("FAKTUROID_LIVE_WRITE") == "1";

        public static string Get(string key) => Values.Value.TryGetValue(key, out var value) ? value : null;

        public static Birko.Fakturoid.Fakturoid CreateClient()
        {
            return new Birko.Fakturoid.Fakturoid(Get("FAKTUROID_CLIENT_ID"), Get("FAKTUROID_CLIENT_SECRET"), Get("FAKTUROID_ACCOUNT_SLUG"),
                "FakturoidAPI.NET-tests", Get("FAKTUROID_CONTACT_EMAIL"));
        }

        private static IReadOnlyDictionary<string, string> Load()
        {
            var path = Environment.GetEnvironmentVariable("FAKTUROID_TEST_ENV")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fakturoid-test.env");
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("Live tests need the credentials file " + path);
            }
            return File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#") && line.Contains('='))
                .Select(line => line.Split('=', 2))
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim().Trim('"'));
        }
    }

    // Skipped unless FAKTUROID_LIVE=1. Read-only.
    public sealed class LiveFactAttribute : FactAttribute
    {
        public LiveFactAttribute()
        {
            if (!LiveSettings.ReadEnabled)
            {
                Skip = "Live test: set FAKTUROID_LIVE=1 and provide ~/.fakturoid-test.env";
            }
        }
    }

    // Skipped unless FAKTUROID_LIVE=1 and FAKTUROID_LIVE_WRITE=1. Creates documents and deletes them again.
    public sealed class LiveWriteFactAttribute : FactAttribute
    {
        public LiveWriteFactAttribute()
        {
            if (!LiveSettings.WriteEnabled)
            {
                Skip = "Live write test: set FAKTUROID_LIVE=1 and FAKTUROID_LIVE_WRITE=1 (test account only)";
            }
        }
    }

    public class LiveTest
    {
        [LiveFact]
        public async Task CurrentUserListsTheConfiguredAccount()
        {
            var client = LiveSettings.CreateClient();

            var user = await client.Users.Current();

            user.Accounts.Select(a => a.Slug).ShouldContain(LiveSettings.Get("FAKTUROID_ACCOUNT_SLUG"));
            client.RateLimit.ShouldNotBeNull();
        }

        [LiveFact]
        public async Task ReadsAccountSubjectsAndInvoices()
        {
            var client = LiveSettings.CreateClient();

            var account = await client.Account.View();
            var subjects = await client.Subjects.List();
            var invoices = await client.Invoices.List();

            account.Subdomain.ShouldNotBeNullOrEmpty();
            subjects.Items.Count.ShouldBeLessThanOrEqualTo(Birko.Fakturoid.Fakturoid.PageSize);
            invoices.Items.Count.ShouldBeLessThanOrEqualTo(Birko.Fakturoid.Fakturoid.PageSize);
        }

        [LiveFact]
        public async Task InvoiceNumberFormatsFitTheirPreviews()
        {
            var client = LiveSettings.CreateClient();

            var formats = await client.NumberFormats.ListInvoices();

            formats.Count(f => f.Default).ShouldBe(1);
            foreach (var format in formats)
            {
                format.Fits(format.Preview.Split(", ")[0]).ShouldBeTrue(format.Format);
            }
        }

        [LiveFact]
        public async Task EventsNeverContainTheSecret()
        {
            var client = LiveSettings.CreateClient();
            client.IncludeBodiesInEvents = true;
            var texts = new List<string>();
            client.RequestCompleted += (s, e) => texts.Add($"{e.Method} {e.Uri} {e.RequestBody} {e.ResponseBody} {e.Exception}");

            await client.Users.Current();
            await Should.ThrowAsync<NotFoundException>(() => client.Invoices.View(int.MaxValue));

            texts.Count.ShouldBe(3);
            texts.ShouldAllBe(t => !t.Contains(LiveSettings.Get("FAKTUROID_CLIENT_SECRET")));
        }

        [LiveWriteFact]
        public async Task SubjectInvoicePdfAndPaymentRoundTrip()
        {
            var client = LiveSettings.CreateClient();
            var marker = "fakturoidapi-test-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Birko.Fakturoid.Response.Subject subject = null;
            Birko.Fakturoid.Response.Invoice invoice = null;
            try
            {
                subject = await client.Subjects.Add(new Req.Subject { Name = marker, CustomId = marker, Email = "test@example.com" });
                (await client.Subjects.FindByCustomId(marker)).Id.ShouldBe(subject.Id);
                await client.Subjects.Edit(subject.Id, new Req.Subject { City = "Praha" });

                invoice = await client.Invoices.Add(new Req.Invoice
                {
                    SubjectId = subject.Id,
                    CustomId = marker,
                    PrivateNote = "Created by FakturoidAPI.NET tests, deleted again.",
                    Lines = new List<Req.InvoiceLine> { new Req.InvoiceLine { Name = "Test", Quantity = 1, UnitPrice = 100, VatRate = 0 } },
                });
                (await client.Invoices.FindByCustomId(marker)).ShouldHaveSingleItem().Id.ShouldBe(invoice.Id);

                var pdf = await client.Invoices.DownloadPdfWhenReady(invoice.Id, maxAttempts: 15);
                pdf.Take(4).ShouldBe(new byte[] { 0x25, 0x50, 0x44, 0x46 });

                var payment = await client.InvoicePayments.Add(invoice.Id, new Req.InvoicePayment { Amount = 100 });
                (await client.Invoices.View(invoice.Id)).Payments.Select(p => p.Id).ShouldContain(payment.Id);
                await client.InvoicePayments.Delete(invoice.Id, payment.Id);
            }
            finally
            {
                if (invoice != null)
                {
                    await client.Invoices.Delete(invoice.Id);
                }
                if (subject != null)
                {
                    await client.Subjects.Delete(subject.Id);
                }
            }
        }
    }
}
