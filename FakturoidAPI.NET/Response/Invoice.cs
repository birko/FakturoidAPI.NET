using Birko.Fakturoid.Json;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Birko.Fakturoid.Response
{
    // An invoice as Fakturoid returns it. Lines, Attachments and Payments hide the request properties of
    // the same names with their response types; to Edit an invoice send a new Request.Invoice with the
    // changes only (lines by Id).
    public class Invoice : Request.Invoice
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("your_name")]
        public string YourName { get; set; }

        [JsonPropertyName("your_street")]
        public string YourStreet { get; set; }

        [JsonPropertyName("your_city")]
        public string YourCity { get; set; }

        [JsonPropertyName("your_zip")]
        public string YourZip { get; set; }

        [JsonPropertyName("your_country")]
        public string YourCountry { get; set; }

        [JsonPropertyName("your_registration_no")]
        public string YourRegistrationNo { get; set; }

        [JsonPropertyName("your_vat_no")]
        public string YourVatNo { get; set; }

        [JsonPropertyName("your_local_vat_no")]
        public string YourLocalVatNo { get; set; }

        // Generator the document was generated from.
        [JsonPropertyName("generator_id")]
        public int? GeneratorId { get; set; }

        // Token of the public webinvoice URL (hidden in RequestCompleted).
        [JsonPropertyName("token")]
        public string Token { get; set; }

        // ValueLists.InvoiceStatus.
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("due_on")]
        [JsonConverter(typeof(DateConverter))]
        public DateTime? DueOn { get; set; }

        [JsonPropertyName("sent_at")]
        public DateTimeOffset? SentAt { get; set; }

        [JsonPropertyName("paid_on")]
        [JsonConverter(typeof(DateConverter))]
        public DateTime? PaidOn { get; set; }

        [JsonPropertyName("reminder_sent_at")]
        public DateTimeOffset? ReminderSentAt { get; set; }

        [JsonPropertyName("last_reminder_sent_at")]
        public DateTimeOffset? LastReminderSentAt { get; set; }

        [JsonPropertyName("due_date_reminder_sent_at")]
        public DateTimeOffset? DueDateReminderSentAt { get; set; }

        [JsonPropertyName("cancelled_at")]
        public DateTimeOffset? CancelledAt { get; set; }

        [JsonPropertyName("uncollectible_at")]
        public DateTimeOffset? UncollectibleAt { get; set; }

        [JsonPropertyName("locked_at")]
        public DateTimeOffset? LockedAt { get; set; }

        [JsonPropertyName("webinvoice_seen_on")]
        [JsonConverter(typeof(DateConverter))]
        public DateTime? WebinvoiceSeenOn { get; set; }

        // Total without VAT.
        [JsonPropertyName("subtotal")]
        public decimal? Subtotal { get; set; }

        // Total with VAT.
        [JsonPropertyName("total")]
        public decimal? Total { get; set; }

        [JsonPropertyName("native_subtotal")]
        public decimal? NativeSubtotal { get; set; }

        [JsonPropertyName("native_total")]
        public decimal? NativeTotal { get; set; }

        [JsonPropertyName("rounding_adjustment")]
        public decimal? RoundingAdjustment { get; set; }

        // After deducting proformas and tax documents, VAT included.
        [JsonPropertyName("remaining_amount")]
        public decimal? RemainingAmount { get; set; }

        [JsonPropertyName("remaining_native_amount")]
        public decimal? RemainingNativeAmount { get; set; }

        // Legacy EET records, kept as raw JSON.
        [JsonPropertyName("eet_records")]
        public List<JsonElement> EetRecords { get; set; }

        [JsonPropertyName("lines")]
        public new List<InvoiceLine> Lines { get; set; }

        [JsonPropertyName("vat_rates_summary")]
        public List<VatRateSummary> VatRatesSummary { get; set; }

        // Final invoice: the paid advances.
        [JsonPropertyName("paid_advances")]
        public List<PaidAdvance> PaidAdvances { get; set; }

        [JsonPropertyName("payments")]
        public List<InvoicePayment> Payments { get; set; }

        [JsonPropertyName("attachments")]
        public new List<Attachment> Attachments { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }

        // Public webinvoice (hidden in RequestCompleted).
        [JsonPropertyName("public_html_url")]
        public string PublicHtmlUrl { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("pdf_url")]
        public string PdfUrl { get; set; }

        [JsonPropertyName("subject_url")]
        public string SubjectUrl { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; }
    }

    public class InvoiceLine : Request.InvoiceLine
    {
        [JsonPropertyName("unit_price_without_vat")]
        public decimal? UnitPriceWithoutVat { get; set; }

        [JsonPropertyName("unit_price_with_vat")]
        public decimal? UnitPriceWithVat { get; set; }

        [JsonPropertyName("total_price_without_vat")]
        public decimal? TotalPriceWithoutVat { get; set; }

        [JsonPropertyName("total_vat")]
        public decimal? TotalVat { get; set; }

        [JsonPropertyName("native_total_price_without_vat")]
        public decimal? NativeTotalPriceWithoutVat { get; set; }

        [JsonPropertyName("native_total_vat")]
        public decimal? NativeTotalVat { get; set; }

        [JsonPropertyName("inventory")]
        public LineInventory Inventory { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; }
    }

    public class LineInventory
    {
        [JsonPropertyName("item_id")]
        public int? ItemId { get; set; }

        [JsonPropertyName("sku")]
        public string Sku { get; set; }

        // ian, ean, isbn.
        [JsonPropertyName("article_number_type")]
        public string ArticleNumberType { get; set; }

        [JsonPropertyName("article_number")]
        public string ArticleNumber { get; set; }

        [JsonPropertyName("move_id")]
        public int? MoveId { get; set; }
    }

    public class VatRateSummary
    {
        [JsonPropertyName("vat_rate")]
        public decimal? VatRate { get; set; }

        [JsonPropertyName("base")]
        public decimal? Base { get; set; }

        [JsonPropertyName("vat")]
        public decimal? Vat { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("native_base")]
        public decimal? NativeBase { get; set; }

        [JsonPropertyName("native_vat")]
        public decimal? NativeVat { get; set; }

        [JsonPropertyName("native_currency")]
        public string NativeCurrency { get; set; }
    }

    public class PaidAdvance
    {
        // Tax document ID.
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("number")]
        public string Number { get; set; }

        [JsonPropertyName("variable_symbol")]
        public string VariableSymbol { get; set; }

        [JsonPropertyName("paid_on")]
        [JsonConverter(typeof(DateConverter))]
        public DateTime? PaidOn { get; set; }

        [JsonPropertyName("vat_rate")]
        public decimal? VatRate { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("vat")]
        public decimal? Vat { get; set; }
    }

    public class Attachment : Request.Attachment
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("content_type")]
        public string ContentType { get; set; }

        // API URL of the file.
        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; }
    }
}
