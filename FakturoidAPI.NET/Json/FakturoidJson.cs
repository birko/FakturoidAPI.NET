using System;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Birko.Fakturoid.Json
{
    internal static class FakturoidJson
    {
        // null properties are not sent, so Edit changes only what was set.
        // Fakturoid sends decimals as strings ("1210.0"), AllowReadingFromString reads them.
        internal static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            // JSON over HTTP, never embedded in HTML; keeps Czech letters readable in RequestCompleted.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        // Serializes by the declared type T: a Response model passed as its Request base sends only
        // the request properties. With T = object the runtime type is used.
        internal static string Serialize<T>(T value)
        {
            return value == null ? null : JsonSerializer.Serialize(value, Options);
        }

        internal static T Deserialize<T>(byte[] utf8Json)
        {
            return JsonSerializer.Deserialize<T>(utf8Json, Options);
        }

        internal static T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
    }

    // Fakturoid "Date" attributes: YYYY-MM-DD.
    public class DateConverter : JsonConverter<DateTime?>
    {
        public override bool HandleNull => true;

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            var text = reader.GetString();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            DateTime date;
            if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return date;
            }
            // Tolerate a full timestamp, keep its date part.
            DateTimeOffset timestamp;
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp))
            {
                return timestamp.Date;
            }
            throw new JsonException(string.Format("'{0}' is not a date (YYYY-MM-DD).", text));
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }
    }

    // A string attribute that Fakturoid may also send as a number (e.g. response_status "404").
    public class StringOrNumberConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                    using (var document = JsonDocument.ParseValue(ref reader))
                    {
                        return document.RootElement.GetRawText();
                    }
                default:
                    throw new JsonException("Expected a string or a number.");
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
