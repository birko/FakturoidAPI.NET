using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Birko.Fakturoid.Response
{
    public class Subject : Request.Subject
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        // User who created the subject.
        [JsonPropertyName("user_id")]
        public int? UserId { get; set; }

        // Unreliable VAT payer.
        [JsonPropertyName("unreliable")]
        public bool? Unreliable { get; set; }

        [JsonPropertyName("unreliable_checked_at")]
        public DateTimeOffset? UnreliableCheckedAt { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("client_portal_url")]
        public string ClientPortalUrl { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        // Attributes Fakturoid added after this library was written.
        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; }
    }
}
