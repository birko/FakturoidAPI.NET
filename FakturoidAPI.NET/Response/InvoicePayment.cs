using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Birko.Fakturoid.Response
{
    public class InvoicePayment : Request.InvoicePayment
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        // Same as the invoice currency.
        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        // Tax document issued for this payment, if any.
        [JsonPropertyName("tax_document_id")]
        public int? TaxDocumentId { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; }
    }
}
