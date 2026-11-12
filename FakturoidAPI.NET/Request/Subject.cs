using System;
using System.Text.Json.Serialization;

namespace Birko.Fakturoid.Request
{
    // Writable attributes of a subject (https://www.fakturoid.cz/api/v3/subjects). null = not sent.
    public class Subject
    {
        // Identifier in your application.
        [JsonPropertyName("custom_id")]
        public string CustomId { get; set; }

        // ValueLists.SubjectType; default customer.
        [JsonPropertyName("type")]
        public string Type { get; set; }

        // Required on create.
        [JsonPropertyName("name")]
        public string Name { get; set; }

        // Contact person name.
        [JsonPropertyName("full_name")]
        public string FullName { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("email_copy")]
        public string EmailCopy { get; set; }

        [JsonPropertyName("phone")]
        public string Phone { get; set; }

        [JsonPropertyName("web")]
        public string Web { get; set; }

        [JsonPropertyName("street")]
        public string Street { get; set; }

        [JsonPropertyName("city")]
        public string City { get; set; }

        [JsonPropertyName("zip")]
        public string Zip { get; set; }

        // ISO code; default from the account.
        [JsonPropertyName("country")]
        public string Country { get; set; }

        // Must be true to set the delivery address; false clears it.
        [JsonPropertyName("has_delivery_address")]
        public bool? HasDeliveryAddress { get; set; }

        [JsonPropertyName("delivery_name")]
        public string DeliveryName { get; set; }

        [JsonPropertyName("delivery_street")]
        public string DeliveryStreet { get; set; }

        [JsonPropertyName("delivery_city")]
        public string DeliveryCity { get; set; }

        [JsonPropertyName("delivery_zip")]
        public string DeliveryZip { get; set; }

        [JsonPropertyName("delivery_country")]
        public string DeliveryCountry { get; set; }

        // Days until an invoice for this subject is due.
        [JsonPropertyName("due")]
        public int? Due { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        // ValueLists.Language.
        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("private_note")]
        public string PrivateNote { get; set; }

        // IČO.
        [JsonPropertyName("registration_no")]
        public string RegistrationNo { get; set; }

        // DIČ / IČ DPH, typically with the country code.
        [JsonPropertyName("vat_no")]
        public string VatNo { get; set; }

        // SK DIČ (Slovakia only, without the country code).
        [JsonPropertyName("local_vat_no")]
        public string LocalVatNo { get; set; }

        // Three-digit legal form code as a string.
        [JsonPropertyName("legal_form")]
        public string LegalForm { get; set; }

        [JsonPropertyName("vat_mode")]
        public string VatMode { get; set; }

        [JsonPropertyName("bank_account")]
        public string BankAccount { get; set; }

        [JsonPropertyName("iban")]
        public string Iban { get; set; }

        [JsonPropertyName("swift_bic")]
        public string SwiftBic { get; set; }

        // Fixed variable symbol used for all invoices of this subject instead of the invoice number.
        [JsonPropertyName("variable_symbol")]
        public string VariableSymbol { get; set; }

        // ValueLists.SettingValue (inherit, on, off).
        [JsonPropertyName("setting_update_from_ares")]
        public string SettingUpdateFromAres { get; set; }

        [Obsolete("Deprecated by Fakturoid in favor of SettingUpdateFromAres.")]
        [JsonPropertyName("ares_update")]
        public bool? AresUpdate { get; set; }

        // ValueLists.SettingValue.
        [JsonPropertyName("setting_invoice_pdf_attachments")]
        public string SettingInvoicePdfAttachments { get; set; }

        // ValueLists.SettingValue.
        [JsonPropertyName("setting_estimate_pdf_attachments")]
        public string SettingEstimatePdfAttachments { get; set; }

        // ValueLists.SettingValue.
        [JsonPropertyName("setting_invoice_send_reminders")]
        public string SettingInvoiceSendReminders { get; set; }

        // ValueLists.SettingValue.
        [JsonPropertyName("setting_invoice_send_due_date_reminders")]
        public string SettingInvoiceSendDueDateReminders { get; set; }

        // Suggest for documents; default true.
        [JsonPropertyName("suggestion_enabled")]
        public bool? SuggestionEnabled { get; set; }

        // Reverse charge default for new invoices in the web app.
        [JsonPropertyName("invoice_reverse_charge")]
        public bool? InvoiceReverseCharge { get; set; }

        [JsonPropertyName("invoice_supply_code")]
        public string InvoiceSupplyCode { get; set; }

        [JsonPropertyName("custom_email_text")]
        public string CustomEmailText { get; set; }

        [JsonPropertyName("overdue_email_text")]
        public string OverdueEmailText { get; set; }

        [JsonPropertyName("due_date_reminder_email_text")]
        public string DueDateReminderEmailText { get; set; }

        [JsonPropertyName("invoice_from_proforma_email_text")]
        public string InvoiceFromProformaEmailText { get; set; }

        [JsonPropertyName("thank_you_email_text")]
        public string ThankYouEmailText { get; set; }

        [JsonPropertyName("custom_estimate_email_text")]
        public string CustomEstimateEmailText { get; set; }

        // null (inherit), "disabled", "recent", "client_portal".
        [JsonPropertyName("webinvoice_history")]
        public string WebinvoiceHistory { get; set; }
    }

    // Subjects.List filter.
    public class SubjectFilter
    {
        // Created after.
        public DateTimeOffset? Since { get; set; }

        // Created or updated after.
        public DateTimeOffset? UpdatedSince { get; set; }

        // Your own identifier.
        public string CustomId { get; set; }

        internal string ToPath(string path, int page)
        {
            return new Query()
                .Add("since", Since)
                .Add("updated_since", UpdatedSince)
                .Add("custom_id", CustomId)
                .Page(page)
                .ToString(path);
        }
    }
}
