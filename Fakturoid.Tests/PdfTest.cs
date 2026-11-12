using Birko.Fakturoid.Exceptions;
using Shouldly;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fakturoid.Tests
{
    // Offline: GET invoices/{id}/download.pdf answers 204 while the PDF is generated, then 200 application/pdf.
    public class PdfTest
    {
        private static readonly byte[] Pdf = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }; // %PDF-1

        private static HttpResponseMessage PdfResponse()
        {
            var content = new ByteArrayContent(Pdf);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }

        [Fact]
        public async Task NotReadyIsDistinctFromAPdf()
        {
            var handler = new StubHandler().EnqueueStatus(HttpStatusCode.NoContent).Enqueue(PdfResponse());
            var client = TestClient.Create(handler);

            var first = await client.Invoices.DownloadPdf(529);
            var second = await client.Invoices.DownloadPdf(529);

            first.IsReady.ShouldBeFalse();
            first.Content.ShouldBeNull();
            second.IsReady.ShouldBeTrue();
            second.Content.ShouldBe(Pdf);
            var request = handler.ApiRequests.First();
            request.PathAndQuery.ShouldBe("/api/v3/accounts/applecorp/invoices/529/download.pdf");
            request.Accept.ShouldBe("application/pdf");
        }

        [Fact]
        public async Task WhenReadyPollsUntilThePdfIsGenerated()
        {
            var handler = new StubHandler()
                .EnqueueStatus(HttpStatusCode.NoContent)
                .EnqueueStatus(HttpStatusCode.NoContent)
                .Enqueue(PdfResponse());
            var client = TestClient.Create(handler);

            var pdf = await client.Invoices.DownloadPdfWhenReady(529, maxAttempts: 5, delay: TimeSpan.FromMilliseconds(10));

            pdf.ShouldBe(Pdf);
            handler.ApiRequests.Count().ShouldBe(3);
        }

        [Fact]
        public async Task WhenReadyGivesUpAfterMaxAttempts()
        {
            var handler = new StubHandler()
                .EnqueueStatus(HttpStatusCode.NoContent)
                .EnqueueStatus(HttpStatusCode.NoContent)
                .EnqueueStatus(HttpStatusCode.NoContent);
            var client = TestClient.Create(handler);

            var ex = await Should.ThrowAsync<PdfNotReadyException>(() => client.Invoices.DownloadPdfWhenReady(529, maxAttempts: 3, delay: TimeSpan.FromMilliseconds(1)));

            ex.Attempts.ShouldBe(3);
            handler.ApiRequests.Count().ShouldBe(3);
        }

        [Fact]
        public async Task CancellationCutsTheWaitBetweenAttemptsShort()
        {
            var handler = new StubHandler().EnqueueStatus(HttpStatusCode.NoContent);
            var client = TestClient.Create(handler);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            var stopwatch = Stopwatch.StartNew();

            await Should.ThrowAsync<OperationCanceledException>(() => client.Invoices.DownloadPdfWhenReady(529, maxAttempts: 10, delay: TimeSpan.FromMinutes(1), cancellationToken: cts.Token));

            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
            handler.ApiRequests.Count().ShouldBe(1);
        }

        [Fact]
        public async Task PdfIsNotPutIntoTheEventBody()
        {
            var handler = new StubHandler().Enqueue(PdfResponse());
            var client = TestClient.Create(handler);
            client.IncludeBodiesInEvents = true;
            Birko.Fakturoid.RequestCompletedEventArgs pdfEvent = null;
            client.RequestCompleted += (s, e) => { if (e.Uri.EndsWith(".pdf")) pdfEvent = e; };

            await client.Invoices.DownloadPdf(1);

            pdfEvent!.ResponseBody.ShouldBeNull();
            pdfEvent.ResponseLength.ShouldBe(Pdf.Length);
        }
    }
}
