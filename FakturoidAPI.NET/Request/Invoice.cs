using Birko.Fakturoid.Json;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Birko.Fakturoid.Request
{
    // Writable attributes of an invoice (https://www.fakturoid.cz/api/v3/invoices). null = not sent, so
    // Edit changes only what was set.
    public class Invoice
    {
        // Identifier in your application. Fakturoid does NOT promise it is unique or that it prevents a
        // duplicate: after a lost answer to Add, look the invoice up (Invoices.FindByCustomId) before
        // sending Add again.
        [JsonPropertyName("custom_id")]
        public string CustomId { get; set; }

        // ValueLists.DocumentType; default invoice.
        [JsonPropertyName("document_type")]
        public string DocumentType { get; set; }

        // ValueLists.ProformaFollowupDocument, for proformas.
        [JsonPropertyName("proforma_followup_document")]
        public string ProformaFollowupDocument { get; set; }

        // Only when creating a final invoice from tax documents.
        [JsonPropertyName("tax_document_ids")]
        public List<int> TaxDocumentIds { get; set; }

        // ID of the corrected invoice (with DocumentType correction).
        [JsonPropertyName("correction_id")]
        public int? CorrectionId { get; set; }

        // Default: the next number of the number format.
        [JsonPropertyName("number")]
        public string Number { get; set; }

        // Create only, forbidden on update.
        [JsonPropertyName("number_format_id")]
        public int? NumberFormatId { get; set; }

        [JsonPropertyName("variable_symbol")]
        public string VariableSymbol { get; set; }

        [JsonPropertyName("client_name")]
        public string ClientName { get; set; }

        [JsonPropertyName("client_street")]
        public string ClientStreet { get; set; }

        [JsonPropertyName("client_city")]
        public string ClientCity { get; set; }

        [JsonPropertyName("client_zip")]
        public string ClientZip { get; set; }

        [JsonPropertyName("client_country")]
        public string ClientCountry { get; set; }

        [JsonPropertyName("client_has_delivery_address")]
        public bool? ClientHasDeliveryAddress { get; set; }

        [JsonPropertyName("client_delivery_name")]
        public string ClientDeliveryName { get; set; }

        [JsonPropertyName("client_delivery_street")]
        public string ClientDeliveryStreet { get; set; }

        [JsonPropertyName("client_delivery_city")]
        public string ClientDeliveryCity { get; set; }

        [JsonPropertyName("client_delivery_zip")]
        public string ClientDeliveryZip { get; set; }

        [JsonPropertyName("client_delivery_country")]
        public string ClientDeliveryCountry { get; set; }

        [JsonPropertyName("client_registration_no")]
        public string ClientRegistrationNo { get; set; }

        [JsonPropertyName("client_vat_no")]
        public string ClientVatNo { get; set; }

        [JsonPropertyName("client_local_vat_no")]
        public string ClientLocalVatNo { get; set; }

        // Required on create.
        [JsonPropertyName("subject_id")]
        public int? SubjectId { get; set; }

        [JsonPropertyName("subject_custom_id")]
        public string SubjectCustomId { get; set; }

        // Proforma: related invoice; invoice: related proforma; tax document: related final invoice.
        [JsonPropertyName("related_id")]
        public int? RelatedId { get; set; }

        [JsonPropertyName("paypal")]
        public bool? Paypal { get; set; }

        [JsonPropertyName("gopay")]
        public bool? Gopay { get; set; }

        [JsonPropertyName("order_number")]
        public string OrderNumber { get; set; }

        [JsonPropertyName("issued_on")]
        [JsonConverter(typeof(DateConverter))]
        public DateTime? IssuedOn { get; set; }

        // Chargeable event date; default the date of issue. (Sending null to reset it is not supported:
        // null properties are never sent.)
        [JsonPropertyName("taxable_fulfillment_due")]
        [JsonConverter(typeof(DateConverter))]
        public DateTime? TaxableFulfillmentDue { get; set; }

        // Days until overdue.
        [JsonPropertyName("due")]
        public int? Due { get; set; }

        // Text before lines.
        [JsonPropertyName("note")]
        public string Note { get; set; }

        [JsonPropertyName("footer_note")]
        public string FooterNote { get; set; }

        [JsonPropertyName("private_note")]
        public string PrivateNote { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }

        // Create only.
        [JsonPropertyName("bank_account_id")]
        public int? BankAccountId { get; set; }

        [JsonPropertyName("bank_account")]
        public string BankAccount { get; set; }

        [JsonPropertyName("iban")]
        public string Iban { get; set; }

        [JsonPropertyName("swift_bic")]
        public string SwiftBic { get; set; }

        // ValueLists.IbanVisibility.
        [JsonPropertyName("iban_visibility")]
        public string IbanVisibility { get; set; }

        [JsonPropertyName("show_already_paid_note_in_pdf")]
        public bool? ShowAlreadyPaidNoteInPdf { get; set; }

        // ValueLists.PaymentMethod.
        [JsonPropertyName("payment_method")]
        public string PaymentMethod { get; set; }

        // Up to 20 characters, only with PaymentMethod custom.
        [JsonPropertyName("custom_payment_method")]
        public string CustomPaymentMethod { get; set; }

        [JsonPropertyName("hide_bank_account")]
        public bool? HideBankAccount { get; set; }

        // ValueLists.Theme.
        [JsonPropertyName("theme")]
        public string Theme { get; set; }

        [JsonPropertyName("qr_payment")]
        public bool? QrPayment { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        // Required when the currency differs from the account currency.
        [JsonPropertyName("exchange_rate")]
        public decimal? ExchangeRate { get; set; }

        // ValueLists.Language.
        [JsonPropertyName("language")]
        public string Language { get; set; }

        // Reverse charge.
        [JsonPropertyName("transferred_tax_liability")]
        public bool? TransferredTaxLiability { get; set; }

        [JsonPropertyName("supply_code")]
        public string SupplyCode { get; set; }

        // ValueLists.Oss.
        [JsonPropertyName("oss")]
        public string Oss { get; set; }

        // ValueLists.VatPriceMode.
        [JsonPropertyName("vat_price_mode")]
        public string VatPriceMode { get; set; }

        // Write-only: round the total (VAT included).
        [JsonPropertyName("round_total")]
        public bool? RoundTotal { get; set; }

        // On Edit: lines with Id are updated, lines without Id are added, Destroy = true deletes a line.
        [JsonPropertyName("lines")]
        public List<InvoiceLine> Lines { get; set; }

        [JsonPropertyName("attachments")]
        public List<Attachment> Attachments { get; set; }
    }

    public class InvoiceLine
    {
        // Required when editing an existing line, otherwise the line is added again.
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        // Required.
        [JsonPropertyName("name")]
        public string Name { get; set; }

        // Default 1.
        [JsonPropertyName("quantity")]
        public decimal? Quantity { get; set; }

        [JsonPropertyName("unit_name")]
        public string UnitName { get; set; }

        // Required; with or without VAT according to Invoice.VatPriceMode.
        [JsonPropertyName("unit_price")]
        public decimal? UnitPrice { get; set; }

        // Default 0.
        [JsonPropertyName("vat_rate")]
        public decimal? VatRate { get; set; }

        // Write-only: link to an inventory item.
        [JsonPropertyName("inventory_item_id")]
        public int? InventoryItemId { get; set; }

        // Write-only: load the line from the inventory item with this SKU (set attributes override it).
        [JsonPropertyName("sku")]
        public string Sku { get; set; }

        // Edit only: true deletes the line with Id.
        [JsonPropertyName("_destroy")]
        public bool? Destroy { get; set; }
    }

    public class Attachment
    {
        // Default attachment.{extension}.
        [JsonPropertyName("filename")]
        public string Filename { get; set; }

        // Data URI, e.g. "data:application/pdf;base64,...".
        [JsonPropertyName("data_url")]
        public string DataUrl { get; set; }

        public static Attachment FromBytes(string filename, string contentType, byte[] content)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                throw new ArgumentException("The MIME type is required.", nameof(contentType));
            }
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }
            return new Attachment
            {
                Filename = filename,
                DataUrl = "data:" + contentType + ";base64," + Convert.ToBase64String(content),
            };
        }
    }

    // Invoices.List filter.
    public class InvoiceFilter
    {
        // Created after.
        public DateTimeOffset? Since { get; set; }

        // Created before.
        public DateTimeOffset? Until { get; set; }

        // Created or updated after.
        public DateTimeOffset? UpdatedSince { get; set; }

        // Created or updated before.
        public DateTimeOffset? UpdatedUntil { get; set; }

        public int? SubjectId { get; set; }

        // Your own identifier.
        public string CustomId { get; set; }

        // Document number, e.g. 2023-0005.
        public string Number { get; set; }

        // ValueLists.InvoiceStatus.
        public string Status { get; set; }

        // ValueLists.DocumentTypeFilter (regular, proforma, invoice, correction, tax_document).
        public string DocumentType { get; set; }

        internal string ToPath(string path, int page)
        {
            return new Query()
                .Add("since", Since)
                .Add("until", Until)
                .Add("updated_since", UpdatedSince)
                .Add("updated_until", UpdatedUntil)
                .Add("subject_id", SubjectId)
                .Add("custom_id", CustomId)
                .Add("number", Number)
                .Add("status", Status)
                .Add("document_type", DocumentType)
                .Page(page)
                .ToString(path);
        }
    }
}
