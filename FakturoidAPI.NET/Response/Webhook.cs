using Birko.Fakturoid.Json;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Birko.Fakturoid.Response
{
    public class Webhook : Request.Webhook
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        // Identifier for Webhooks.FailedDeliveries.
        [JsonPropertyName("failed_deliveries_uuid")]
        public string FailedDeliveriesUuid { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; }
    }

    // A webhook event that failed all delivery attempts (kept 60 days).
    public class FailedWebhookDelivery
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("event_name")]
        public string EventName { get; set; }

        // Sent as the Idempotency-Key header of the deliveries.
        [JsonPropertyName("idempotency_key")]
        public string IdempotencyKey { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        // The payload that was sent (see WebhookPayload).
        [JsonPropertyName("body")]
        public JsonElement Body { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        // Most recent first.
        [JsonPropertyName("deliveries")]
        public List<WebhookDeliveryAttempt> Deliveries { get; set; }
    }

    public class WebhookDeliveryAttempt
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("request_id")]
        public string RequestId { get; set; }

        // HTTP status code (e.g. "404") or ssl_error, connection_failed, timeout, unknown_error.
        [JsonPropertyName("response_status")]
        [JsonConverter(typeof(StringOrNumberConverter))]
        public string ResponseStatus { get; set; }

        [JsonPropertyName("response_content_type")]
        public string ResponseContentType { get; set; }

        [JsonPropertyName("response_body")]
        public string ResponseBody { get; set; }

        [JsonPropertyName("started_at")]
        public DateTimeOffset? StartedAt { get; set; }

        [JsonPropertyName("finished_at")]
        public DateTimeOffset? FinishedAt { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }
    }

    // Body of a webhook delivery (POST from Fakturoid to Webhook.WebhookUrl); read it with WebhookReceiver.Parse.
    public class WebhookPayload
    {
        [JsonPropertyName("webhook_id")]
        public int WebhookId { get; set; }

        // ValueLists.WebhookEvent.
        [JsonPropertyName("event_name")]
        public string EventName { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        // The resources of the event, e.g. {"invoice": {...}, "payment": {...}}.
        [JsonPropertyName("body")]
        public JsonElement Body { get; set; }

        // The body property of this name as T, or default when it is missing.
        public T GetBody<T>(string name)
        {
            JsonElement value;
            if (Body.ValueKind != JsonValueKind.Object || !Body.TryGetProperty(name, out value) || value.ValueKind == JsonValueKind.Null)
            {
                return default(T);
            }
            return FakturoidJson.Deserialize<T>(value.GetRawText());
        }

        // invoice_* events and recurring_generator_invoice_created.
        public Invoice Invoice => GetBody<Invoice>("invoice");

        // invoice_paid, invoice_payment_added.
        public InvoicePayment Payment => GetBody<InvoicePayment>("payment");

        // invoice_payment_removed.
        public List<InvoicePayment> Payments => GetBody<List<InvoicePayment>>("payments");

        // subject_* events.
        public Subject Subject => GetBody<Subject>("subject");
    }
}
