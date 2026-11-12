using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Birko.Fakturoid.Request
{
    // https://www.fakturoid.cz/api/v3/webhooks. Paid plans only; with client credentials, webhook management
    // must be enabled in the user's Settings > User account. Credentials manage only their own webhooks.
    public class Webhook
    {
        // URL Fakturoid POSTs the events to.
        [JsonPropertyName("webhook_url")]
        public string WebhookUrl { get; set; }

        // Sent as the Authorization header of every delivery; check it with WebhookReceiver.VerifyAuthorization.
        // Hidden in RequestCompleted.
        [JsonPropertyName("auth_header")]
        public string AuthHeader { get; set; }

        [JsonPropertyName("active")]
        public bool? Active { get; set; }

        // ValueLists.WebhookEvent; must not be empty.
        [JsonPropertyName("events")]
        public List<string> Events { get; set; }
    }
}
