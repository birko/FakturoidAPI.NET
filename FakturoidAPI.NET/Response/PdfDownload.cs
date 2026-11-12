namespace Birko.Fakturoid.Response
{
    // Invoices.DownloadPdf: Fakturoid answers 204 while the PDF is being generated.
    public sealed class PdfDownload
    {
        public static readonly PdfDownload NotReady = new PdfDownload(null);

        public PdfDownload(byte[] content)
        {
            Content = content;
        }

        // false: still generating (204), try again in a second or two.
        public bool IsReady => Content != null;

        // The PDF, null when not ready.
        public byte[] Content { get; }
    }
}
