using System.Text.RegularExpressions;

namespace Birko.Fakturoid
{
    // Values hidden as "***" in RequestCompleted bodies and exception texts: OAuth tokens, the webhook
    // auth_header (the secret the receiver checks), and the invoice token / public_html_url (a public
    // link to the invoice).
    internal static class Secrets
    {
        private static readonly Regex SecretInJson = new Regex(
            "(\"(?:access_token|refresh_token|client_secret|token|auth_header|public_html_url)\"\\s*:\\s*)\"(?:[^\"\\\\]|\\\\.)*\"",
            RegexOptions.Compiled);

        internal static string HideInJson(string json)
        {
            return json == null ? null : SecretInJson.Replace(json, "$1\"***\"");
        }

        internal static string Excerpt(string text, int length = 500)
        {
            var hidden = HideInJson(text);
            return hidden == null || hidden.Length <= length ? hidden : hidden.Substring(0, length) + "…";
        }
    }
}
