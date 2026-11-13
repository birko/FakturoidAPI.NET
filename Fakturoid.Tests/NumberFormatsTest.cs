using Birko.Fakturoid.Exceptions;
using Birko.Fakturoid.Response;
using Shouldly;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Fakturoid.Tests
{
    // Offline: number formats against the answer recorded on the test account (2026-11-13).
    public class NumberFormatsTest
    {
        [Fact]
        public async Task ListInvoicesReadsTheRecordedAnswer()
        {
            var handler = new StubHandler()
                .EnqueueJson(HttpStatusCode.OK, TestClient.Data("number-formats-invoices.json"))
                .EnqueueJson(HttpStatusCode.OK, "[]");
            var client = TestClient.Create(handler);

            var formats = await client.NumberFormats.ListInvoices();

            handler.ApiRequests.Select(r => r.PathAndQuery).ShouldBe(new[]
            {
                "/api/v3/accounts/applecorp/number_formats/invoices.json",
                "/api/v3/accounts/applecorp/number_formats/invoices.json?page=2",
            });
            handler.ApiRequests.ShouldAllBe(r => r.Method == HttpMethod.Get);
            formats.Count.ShouldBe(2);
            formats[0].Id.ShouldBe(1658084);
            formats[0].Format.ShouldBe("FV#yyyy##dddddd#");
            formats[0].Preview.ShouldBe("FV2026000001, FV2026000002, ..., FV2026999999");
            formats[0].Default.ShouldBeFalse();
            formats[0].CreatedAt.ShouldBeNull();
            formats[1].Id.ShouldBe(1656503);
            formats[1].Default.ShouldBeTrue();
            formats.ShouldAllBe(f => f.ExtensionData == null || f.ExtensionData.Count == 0);
        }

        [Fact]
        public async Task ListInvoicesReadsTimestamps()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.OK,
                "[{\"id\":1,\"format\":\"#yyyy#-#dddd#\",\"preview\":\"2024-0001, 2024-0002, ...\",\"default\":true," +
                "\"created_at\":\"2024-01-01T12:00:00.000+01:00\",\"updated_at\":\"2024-02-01T12:00:00.000+01:00\"}]")
                .EnqueueJson(HttpStatusCode.OK, "[]");
            var client = TestClient.Create(handler);

            var format = (await client.NumberFormats.ListInvoices()).ShouldHaveSingleItem();

            format.CreatedAt.ShouldBe(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.FromHours(1)));
            format.UpdatedAt.ShouldBe(new DateTimeOffset(2024, 2, 1, 12, 0, 0, TimeSpan.FromHours(1)));
        }

        [Fact]
        public async Task ListInvoicesReadsEveryPage()
        {
            string Formats(int from, int count) => "[" + string.Join(",", Enumerable.Range(from, count)
                .Select(i => $"{{\"id\":{i},\"format\":\"#yyyy#-#dddd#\",\"preview\":\"\",\"default\":{(i == 1 ? "true" : "false")}}}")) + "]";
            var handler = new StubHandler()
                .EnqueueJson(HttpStatusCode.OK, Formats(1, 40))
                .EnqueueJson(HttpStatusCode.OK, Formats(41, 3))
                .EnqueueJson(HttpStatusCode.OK, "[]");
            var client = TestClient.Create(handler);

            var formats = await client.NumberFormats.ListInvoices();

            formats.Select(f => f.Id).ShouldBe(Enumerable.Range(1, 43));
            handler.ApiRequests.Last().PathAndQuery.ShouldEndWith("invoices.json?page=3");
        }

        [Fact]
        public async Task ListInvoicesMapsErrors()
        {
            var handler = new StubHandler().EnqueueJson(HttpStatusCode.Forbidden, "{\"error\":\"forbidden\"}");
            var client = TestClient.Create(handler);

            var ex = await Should.ThrowAsync<ForbiddenException>(() => client.NumberFormats.ListInvoices());

            ex.StatusCode.ShouldBe(403);
        }

        [Fact]
        public async Task ListInvoicesNeedsTheSlug()
        {
            var client = TestClient.Create(new StubHandler(), slug: null);

            await Should.ThrowAsync<InvalidOperationException>(() => client.NumberFormats.ListInvoices());
        }

        [Theory]
        // Measured 2026-11-13: FV2026000002 refused under #yyyy#-#dddd#, accepted under FV#yyyy##dddddd#.
        [InlineData("FV#yyyy##dddddd#", "FV2026000002", true)]
        [InlineData("#yyyy#-#dddd#", "FV2026000002", false)]
        [InlineData("#yyyy#-#dddd#", "2026-0001", true)]
        [InlineData("#yyyy#-#dddd#", "2026-001", false)]
        [InlineData("#yyyy#-#dddd#", "2026-00001", false)]
        [InlineData("#yyyy#-#dddd#", "2026/0001", false)]
        [InlineData("#yyyy#-#dddd#", " 2026-0001", false)]
        [InlineData("FV#yyyy##dddddd#", "fv2026000002", false)]
        [InlineData("#yy##mm#/#ddd#", "2611/042", true)]
        [InlineData("#yy##mm#/#ddd#", "2613/042", false)]
        [InlineData("#yy##mm#/#ddd#", "2600/042", false)]
        [InlineData("F.#yyyy#.#d#", "F.2026.7", true)]
        [InlineData("F.#yyyy#.#d#", "FX2026X7", false)]
        [InlineData("#yyyy#-#dddd#", "", false)]
        [InlineData("#yyyy#-#dddd#", null, false)]
        public void FitsChecksTheShapeOfTheNumber(string format, string number, bool fits)
        {
            NumberFormat.Fits(format, number).ShouldBe(fits);
            new NumberFormat { Format = format }.Fits(number).ShouldBe(fits);
        }

        [Fact]
        public void FitsNeedsAFormat()
        {
            Should.Throw<ArgumentNullException>(() => NumberFormat.Fits(null, "2026-0001"));
        }
    }
}
