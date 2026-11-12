using Birko.Fakturoid.Exceptions;
using Birko.Fakturoid.Json;
using Birko.Fakturoid.Response;
using System;
using System.Text;

namespace Birko.Fakturoid
{
    // Helps the receiver of webhook deliveries (POST from Fakturoid to Webhook.WebhookUrl).
    // Fakturoid does not sign deliveries: it sends the webhook's auth_header as the Authorization header,
    // and an Idempotency-Key (UUID v7, same across retries). Answer 2xx within 30 seconds, otherwise the
    // delivery is retried up to 5 times; deduplicate by the Idempotency-Key.
    public static class WebhookReceiver
    {
        public const string AuthorizationHeader = "Authorization";
        public const string IdempotencyKeyHeader = "Idempotency-Key";

        // true when the received Authorization header equals the auth_header the webhook was created with.
        // Constant-time comparison. An empty expected value is refused: a webhook without auth_header
        // cannot be verified.
        public static bool VerifyAuthorization(string receivedAuthorizationHeader, string expectedAuthHeader)
        {
            if (string.IsNullOrEmpty(expectedAuthHeader))
            {
                throw new ArgumentException("Set auth_header on the webhook and pass it here; without it a delivery cannot be verified.", nameof(expectedAuthHeader));
            }
            if (receivedAuthorizationHeader == null)
            {
                return false;
            }
            var received = Encoding.UTF8.GetBytes(receivedAuthorizationHeader);
            var expected = Encoding.UTF8.GetBytes(expectedAuthHeader);
            var difference = received.Length ^ expected.Length;
            for (var i = 0; i < expected.Length; i++)
            {
                difference |= expected[i] ^ (i < received.Length ? received[i] : 0);
            }
            return difference == 0;
        }

        // Reads the delivery body. Throws ParseException when it is not a webhook payload.
        public static WebhookPayload Parse(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new ParseException("The webhook body is empty.", null, null, "POST", null, null);
            }
            try
            {
                var payload = FakturoidJson.Deserialize<WebhookPayload>(body);
                if (payload == null || string.IsNullOrEmpty(payload.EventName))
                {
                    throw new ParseException("The webhook body has no event_name.", null, null, "POST", null, Secrets.Excerpt(body));
                }
                return payload;
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new ParseException("The webhook body is not the expected JSON: " + ex.Message, ex, null, "POST", null, Secrets.Excerpt(body));
            }
        }

        public static WebhookPayload Parse(byte[] body)
        {
            return Parse(body == null ? null : Encoding.UTF8.GetString(body));
        }
    }
}
