using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Birko.Fakturoid
{
    // https://www.fakturoid.cz/api/v3/number-formats. Read-only: formats are created and changed in the web
    // interface. Only unarchived formats are returned.
    public class NumberFormats
    {
        private readonly Fakturoid fakturoid;

        public NumberFormats(Fakturoid fakturoid)
        {
            this.fakturoid = fakturoid;
        }

        // All number formats of invoices. Pass NumberFormat.Id as Request.Invoice.NumberFormatId when creating an
        // invoice; an invoice with its own Number must fit that format (NumberFormat.Fits), else 422.
        // The docs show no paging, but the endpoint pages (measured 2026-11-13: ?page=2 is empty), so this reads
        // pages until an empty one.
        public Task<List<Response.NumberFormat>> ListInvoices(CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.GetAllPages<Response.NumberFormat>(InvoicesPath, int.MaxValue, cancellationToken);
        }

        private string InvoicesPath(int page)
        {
            return new Query().Page(page).ToString(fakturoid.AccountPath("number_formats/invoices.json"));
        }
    }
}
