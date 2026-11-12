using Birko.Fakturoid.Exceptions;
using Birko.Fakturoid.Request;
using Birko.Fakturoid.Response;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Birko.Fakturoid
{
    // https://www.fakturoid.cz/api/v3/invoices (invoices, proformas, corrections and tax documents).
    public class Invoices
    {
        private readonly Fakturoid fakturoid;

        public Invoices(Fakturoid fakturoid)
        {
            this.fakturoid = fakturoid;
        }

        // One page (40 records) of documents matching the filter.
        public Task<Page<Response.Invoice>> List(InvoiceFilter filter = null, int page = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.GetPage<Response.Invoice>(Path(filter, page), page, cancellationToken);
        }

        // All pages, until an empty page or maxPages.
        public Task<List<Response.Invoice>> ListAll(InvoiceFilter filter = null, int maxPages = int.MaxValue, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.GetAllPages<Response.Invoice>(page => Path(filter, page), maxPages, cancellationToken);
        }

        // Invoices with this custom_id (all pages). Fakturoid does not make custom_id unique, so this is
        // a list. Use it after a lost answer to Add before sending Add again.
        public Task<List<Response.Invoice>> FindByCustomId(string customId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(customId))
            {
                throw new ArgumentException("custom_id is required.", nameof(customId));
            }
            return ListAll(new InvoiceFilter { CustomId = customId }, int.MaxValue, cancellationToken);
        }

        // Fulltext in number, variable symbol, client name, notes and lines; tags match tags.
        public Task<Page<Response.Invoice>> Search(string query, IEnumerable<string> tags = null, int page = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var path = new Query().Add("query", query).Add("tags", tags).Page(page).ToString(fakturoid.AccountPath("invoices/search.json"));
            return fakturoid.GetPage<Response.Invoice>(path, page, cancellationToken);
        }

        public Task<Response.Invoice> View(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.Get<Response.Invoice>(ItemPath(id, ".json"), cancellationToken);
        }

        // POST invoices.json -> 201. Throws MissingBankAccountException when the account has no bank account
        // and ValidationException (422) for invalid data. Not idempotent: see Request.Invoice.CustomId.
        public Task<Response.Invoice> Add(Request.Invoice invoice, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (invoice == null)
            {
                throw new ArgumentNullException(nameof(invoice));
            }
            return fakturoid.Send<Request.Invoice, Response.Invoice>(HttpMethod.Post, fakturoid.AccountPath("invoices.json"), invoice, cancellationToken);
        }

        // PATCH: only the properties that are set change. A locked invoice throws DocumentLockedException.
        public Task<Response.Invoice> Edit(int id, Request.Invoice invoice, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (invoice == null)
            {
                throw new ArgumentNullException(nameof(invoice));
            }
            return fakturoid.Send<Request.Invoice, Response.Invoice>(new HttpMethod("PATCH"), ItemPath(id, ".json"), invoice, cancellationToken);
        }

        // 422 (ValidationException) when it cannot be deleted (locked, has a correction, paid proforma, ...).
        public Task Delete(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.SendWithoutResult(HttpMethod.Delete, ItemPath(id, ".json"), cancellationToken);
        }

        // POST fire.json?event=...; ValueLists.InvoiceAction. 422 when the action cannot be fired.
        public Task Fire(int id, string action, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(action))
            {
                throw new ArgumentException("The action (event) is required.", nameof(action));
            }
            var path = new Query().Add("event", action).ToString(ItemPath(id, "/fire.json"));
            return fakturoid.SendWithoutResult(HttpMethod.Post, path, cancellationToken);
        }

        public Task MarkAsSent(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Fire(id, ValueLists.InvoiceAction.MarkAsSent, cancellationToken);
        }

        public Task Cancel(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Fire(id, ValueLists.InvoiceAction.Cancel, cancellationToken);
        }

        public Task UndoCancel(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Fire(id, ValueLists.InvoiceAction.UndoCancel, cancellationToken);
        }

        public Task Lock(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Fire(id, ValueLists.InvoiceAction.Lock, cancellationToken);
        }

        public Task Unlock(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Fire(id, ValueLists.InvoiceAction.Unlock, cancellationToken);
        }

        public Task MarkAsUncollectible(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Fire(id, ValueLists.InvoiceAction.MarkAsUncollectible, cancellationToken);
        }

        public Task UndoUncollectible(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Fire(id, ValueLists.InvoiceAction.UndoUncollectible, cancellationToken);
        }

        // One attempt: PdfDownload.NotReady while Fakturoid is still generating the PDF (204), else the PDF.
        public async Task<PdfDownload> DownloadPdf(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            var path = ItemPath(id, "/download.pdf");
            var response = await fakturoid.GetBinary(path, "application/pdf", cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return PdfDownload.NotReady;
            }
            if (response.Body.Length == 0)
            {
                throw new ParseException(string.Format("GET {0} answered {1} without a PDF.", path, (int)response.StatusCode),
                    null, (int)response.StatusCode, response.Method, path, null);
            }
            return new PdfDownload(response.Body);
        }

        // Polls DownloadPdf: up to maxAttempts requests, waiting delay (default 2 s) between them.
        // Throws PdfNotReadyException when the PDF is still not ready; the token also cuts the waiting short.
        public async Task<byte[]> DownloadPdfWhenReady(int id, int maxAttempts = 10, TimeSpan? delay = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (maxAttempts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            }
            var wait = delay ?? TimeSpan.FromSeconds(2);
            for (var attempt = 1; ; attempt++)
            {
                var pdf = await DownloadPdf(id, cancellationToken).ConfigureAwait(false);
                if (pdf.IsReady)
                {
                    return pdf.Content;
                }
                if (attempt >= maxAttempts)
                {
                    throw new PdfNotReadyException(string.Format("The PDF of invoice {0} was not ready after {1} attempts.", id, attempt),
                        "GET", ItemPath(id, "/download.pdf"), attempt);
                }
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
        }

        private string Path(InvoiceFilter filter, int page)
        {
            return (filter ?? new InvoiceFilter()).ToPath(fakturoid.AccountPath("invoices.json"), page);
        }

        private string ItemPath(int id, string suffix)
        {
            return fakturoid.AccountPath("invoices/" + id.ToString(CultureInfo.InvariantCulture) + suffix);
        }
    }
}
