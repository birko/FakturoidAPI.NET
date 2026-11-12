using Birko.Fakturoid.Json;
using System;
using System.Text.Json.Serialization;

namespace Birko.Fakturoid.Request
{
    // https://www.fakturoid.cz/api/v3/invoice-payments; every attribute is optional.
    public class InvoicePayment
    {
        // Default today.
        [JsonPropertyName("paid_on")]
        [JsonConverter(typeof(DateConverter))]
        public DateTime? PaidOn { get; set; }

        // In the document currency; default the remaining amount.
        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; }

        // In the account currency; default the remaining amount converted.
        [JsonPropertyName("native_amount")]
        public decimal? NativeAmount { get; set; }

        // Default true when the payments reach the remaining amount.
        [JsonPropertyName("mark_document_as_paid")]
        public bool? MarkDocumentAsPaid { get; set; }

        // Proformas only, with MarkDocumentAsPaid true; ValueLists.ProformaFollowupDocument.
        [JsonPropertyName("proforma_followup_document")]
        public string ProformaFollowupDocument { get; set; }

        // With MarkDocumentAsPaid true; default from the account settings.
        [JsonPropertyName("send_thank_you_email")]
        public bool? SendThankYouEmail { get; set; }

        // Default the invoice variable symbol.
        [JsonPropertyName("variable_symbol")]
        public string VariableSymbol { get; set; }

        // Default the invoice bank account or the default bank account.
        [JsonPropertyName("bank_account_id")]
        public int? BankAccountId { get; set; }
    }
}
