using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Birko.Fakturoid.Response
{
    // https://www.fakturoid.cz/api/v3/users
    public class User
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("full_name")]
        public string FullName { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        // Default account slug (current user only).
        [JsonPropertyName("default_account")]
        public string DefaultAccount { get; set; }

        // Permission for the current account (account users list).
        [JsonPropertyName("permission")]
        public string Permission { get; set; }

        // reports, expenses, invoices.
        [JsonPropertyName("allowed_scope")]
        public List<string> AllowedScope { get; set; }

        // Accounts the user has access to (current user only).
        [JsonPropertyName("accounts")]
        public List<UserAccount> Accounts { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; }
    }

    public class UserAccount
    {
        // Goes to https://app.fakturoid.cz/api/v3/accounts/{slug}/... (Fakturoid.AccountSlug).
        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("registration_no")]
        public string RegistrationNo { get; set; }

        [JsonPropertyName("permission")]
        public string Permission { get; set; }

        [JsonPropertyName("allowed_scope")]
        public List<string> AllowedScope { get; set; }
    }
}
