using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Birko.Fakturoid.Response
{
    // https://www.fakturoid.cz/api/v3/account; read-only, change it in the web interface.
    public class Account
    {
        [JsonPropertyName("subdomain")]
        public string Subdomain { get; set; }

        [JsonPropertyName("plan")]
        public string Plan { get; set; }

        [JsonPropertyName("plan_price")]
        public decimal? PlanPrice { get; set; }

        [JsonPropertyName("plan_paid_users")]
        public int? PlanPaidUsers { get; set; }

        // Monthly API request quota.
        [JsonPropertyName("api_calls_limit")]
        public int? ApiCallsLimit { get; set; }

        // API requests this month, updated hourly.
        [JsonPropertyName("api_calls_used")]
        public int? ApiCallsUsed { get; set; }

        [JsonPropertyName("invoice_email")]
        public string InvoiceEmail { get; set; }

        [JsonPropertyName("phone")]
        public string Phone { get; set; }

        [JsonPropertyName("web")]
        public string Web { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("full_name")]
        public string FullName { get; set; }

        [JsonPropertyName("registration_no")]
        public string RegistrationNo { get; set; }

        [JsonPropertyName("vat_no")]
        public string VatNo { get; set; }

        [JsonPropertyName("local_vat_no")]
        public string LocalVatNo { get; set; }

        // vat_payer, non_vat_payer, identified_person.
        [JsonPropertyName("vat_mode")]
        public string VatMode { get; set; }

        [JsonPropertyName("vat_price_mode")]
        public string VatPriceMode { get; set; }

        [JsonPropertyName("street")]
        public string Street { get; set; }

        [JsonPropertyName("city")]
        public string City { get; set; }

        [JsonPropertyName("zip")]
        public string Zip { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("unit_name")]
        public string UnitName { get; set; }

        [JsonPropertyName("vat_rate")]
        public decimal? VatRate { get; set; }

        [JsonPropertyName("displayed_note")]
        public string DisplayedNote { get; set; }

        [JsonPropertyName("invoice_note")]
        public string InvoiceNote { get; set; }

        [JsonPropertyName("due")]
        public int? Due { get; set; }

        [JsonPropertyName("invoice_language")]
        public string InvoiceLanguage { get; set; }

        // null means bank.
        [JsonPropertyName("invoice_payment_method")]
        public string InvoicePaymentMethod { get; set; }

        [JsonPropertyName("invoice_proforma")]
        public bool? InvoiceProforma { get; set; }

        [JsonPropertyName("invoice_hide_bank_account_for_payments")]
        public List<string> InvoiceHideBankAccountForPayments { get; set; }

        [JsonPropertyName("fixed_exchange_rate")]
        public bool? FixedExchangeRate { get; set; }

        [JsonPropertyName("invoice_selfbilling")]
        public bool? InvoiceSelfbilling { get; set; }

        [JsonPropertyName("default_estimate_type")]
        public string DefaultEstimateType { get; set; }

        [JsonPropertyName("send_overdue_email")]
        public bool? SendOverdueEmail { get; set; }

        [JsonPropertyName("overdue_email_days")]
        public int? OverdueEmailDays { get; set; }

        [JsonPropertyName("send_repeated_reminders")]
        public bool? SendRepeatedReminders { get; set; }

        [JsonPropertyName("send_due_date_reminder_email")]
        public bool? SendDueDateReminderEmail { get; set; }

        [JsonPropertyName("due_date_reminder_email_days")]
        public int? DueDateReminderEmailDays { get; set; }

        [JsonPropertyName("send_invoice_from_proforma_email")]
        public bool? SendInvoiceFromProformaEmail { get; set; }

        [JsonPropertyName("send_thank_you_email")]
        public bool? SendThankYouEmail { get; set; }

        [JsonPropertyName("invoice_paypal")]
        public bool? InvoicePaypal { get; set; }

        [JsonPropertyName("invoice_gopay")]
        public bool? InvoiceGopay { get; set; }

        [JsonPropertyName("digitoo_enabled")]
        public bool? DigitooEnabled { get; set; }

        [JsonPropertyName("digitoo_auto_processing_enabled")]
        public bool? DigitooAutoProcessingEnabled { get; set; }

        [JsonPropertyName("digitoo_remaining_extractions")]
        public int? DigitooRemainingExtractions { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; }
    }
}
