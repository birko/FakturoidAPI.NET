using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Birko.Fakturoid
{
    // https://www.fakturoid.cz/api/v3/invoice-payments. Payments are read with the invoice
    // (Invoices.View(id).Payments); there is no list endpoint.
    public class InvoicePayments
    {
        private readonly Fakturoid fakturoid;

        public InvoicePayments(Fakturoid fakturoid)
        {
            this.fakturoid = fakturoid;
        }

        // POST -> 201. An empty payment pays the remaining amount today. 403 (ForbiddenException) when the
        // invoice is already paid.
        public Task<Response.InvoicePayment> Add(int invoiceId, Request.InvoicePayment payment = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.Send<Request.InvoicePayment, Response.InvoicePayment>(HttpMethod.Post, PaymentsPath(invoiceId, ".json"),
                payment ?? new Request.InvoicePayment(), cancellationToken);
        }

        // Issues a tax document for the payment; Response.InvoicePayment.TaxDocumentId is its invoice id.
        public Task<Response.InvoicePayment> CreateTaxDocument(int invoiceId, int paymentId, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.Send<object, Response.InvoicePayment>(HttpMethod.Post,
                PaymentsPath(invoiceId, "/" + paymentId.ToString(CultureInfo.InvariantCulture) + "/create_tax_document.json"), null, cancellationToken);
        }

        // 403 (ForbiddenException) e.g. on a cancelled invoice, 422 when it cannot be deleted.
        public Task Delete(int invoiceId, int paymentId, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.SendWithoutResult(HttpMethod.Delete,
                PaymentsPath(invoiceId, "/" + paymentId.ToString(CultureInfo.InvariantCulture) + ".json"), cancellationToken);
        }

        private string PaymentsPath(int invoiceId, string suffix)
        {
            return fakturoid.AccountPath("invoices/" + invoiceId.ToString(CultureInfo.InvariantCulture) + "/payments" + suffix);
        }
    }
}
