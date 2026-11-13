using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Birko.Fakturoid.Response
{
    // Read-only; see NumberFormats.
    public class NumberFormat
    {
        private static readonly Regex Placeholder = new Regex("#(yyyy|yy|mm|d+)#", RegexOptions.CultureInvariant);

        [JsonPropertyName("id")]
        public int Id { get; set; }

        // E.g. "FV#yyyy##dddddd#": #yyyy# year, #yy# two-digit year, #mm# month, #d…# the counter with that
        // many digits, anything else literal.
        [JsonPropertyName("format")]
        public string Format { get; set; }

        // E.g. "FV2026000001, FV2026000002, ..., FV2026999999".
        [JsonPropertyName("preview")]
        public string Preview { get; set; }

        // The format of invoices created without NumberFormatId.
        [JsonPropertyName("default")]
        public bool Default { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; }

        // Whether number has the shape of this format.
        public bool Fits(string number)
        {
            return Fits(Format, number);
        }

        // Whether number has the shape of format: the placeholders as digits (#mm# 01-12, #d…# exactly that many
        // digits), the rest literally. Checks the shape only, not that the year or month match the issue date
        // or that the counter is the next one. An unknown #…# is taken literally.
        public static bool Fits(string format, string number)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }
            if (string.IsNullOrEmpty(number))
            {
                return false;
            }
            var pattern = new StringBuilder("^");
            var position = 0;
            foreach (Match match in Placeholder.Matches(format))
            {
                pattern.Append(Regex.Escape(format.Substring(position, match.Index - position)));
                var token = match.Groups[1].Value;
                switch (token)
                {
                    case "yyyy":
                        pattern.Append("[0-9]{4}");
                        break;
                    case "yy":
                        pattern.Append("[0-9]{2}");
                        break;
                    case "mm":
                        pattern.Append("(?:0[1-9]|1[0-2])");
                        break;
                    default:
                        pattern.Append("[0-9]{").Append(token.Length).Append('}');
                        break;
                }
                position = match.Index + match.Length;
            }
            pattern.Append(Regex.Escape(format.Substring(position))).Append('$');
            return Regex.IsMatch(number, pattern.ToString(), RegexOptions.CultureInvariant);
        }
    }
}
