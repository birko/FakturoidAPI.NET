using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Birko.Fakturoid.Exceptions
{
    // HTTP error response -> typed exception.
    internal static class ErrorMapper
    {
        internal static ApiException Create(RawResponse raw, RateLimit rateLimit, RetryConditionHeaderValue retryAfter, bool tokenRequest, DateTimeOffset now)
        {
            var status = (int)raw.StatusCode;
            string error = null;
            string description = null;
            Dictionary<string, IReadOnlyList<string>> errors = null;
            ReadBody(raw.Body, ref error, ref description, ref errors);
            // Token responses are never kept: a successful one would hold a token, keep the rule simple.
            var body = tokenRequest ? null : Secrets.HideInJson(raw.Text);
            var method = raw.Method;
            var uri = raw.Path;

            if (tokenRequest && (status == 400 || status == 401))
            {
                return new AuthenticationException(status, method, uri, error, description, errors, body, rateLimit);
            }
            switch (status)
            {
                case 401:
                    return new AuthenticationException(status, method, uri, error, description, errors, body, rateLimit);
                case 402:
                    return new AccountBlockedException(status, method, uri, error, description, errors, body, rateLimit);
                case 403:
                    if (errors != null && errors.ContainsKey("bank_account"))
                    {
                        return new MissingBankAccountException(status, method, uri, error, description, errors, body, rateLimit);
                    }
                    if (error == "document_locked")
                    {
                        return new DocumentLockedException(status, method, uri, error, description, errors, body, rateLimit);
                    }
                    return new ForbiddenException(status, method, uri, error, description, errors, body, rateLimit);
                case 404:
                    return new NotFoundException(status, method, uri, error, description, errors, body, rateLimit);
                case 422:
                    return new ValidationException(status, method, uri, error, description, errors, body, rateLimit);
                case 429:
                    return new RateLimitExceededException(status, method, uri, error, description, errors, body, rateLimit,
                        ResetAt(rateLimit, retryAfter, now));
                case 503:
                    return new ServiceUnavailableException(status, method, uri, error, description, errors, body, rateLimit);
                default:
                    return new ApiException(status, method, uri, error, description, errors, body, rateLimit);
            }
        }

        private static DateTimeOffset? ResetAt(RateLimit rateLimit, RetryConditionHeaderValue retryAfter, DateTimeOffset now)
        {
            if (rateLimit?.ResetAt != null)
            {
                return rateLimit.ResetAt;
            }
            if (retryAfter?.Delta != null)
            {
                return now + retryAfter.Delta.Value;
            }
            return retryAfter?.Date;
        }

        // Error bodies that are not JSON (e.g. a proxy page) keep only the status code.
        private static void ReadBody(byte[] body, ref string error, ref string description, ref Dictionary<string, IReadOnlyList<string>> errors)
        {
            if (body == null || body.Length == 0)
            {
                return;
            }
            try
            {
                using (var document = JsonDocument.Parse(body))
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        return;
                    }
                    JsonElement value;
                    if (root.TryGetProperty("error", out value))
                    {
                        error = Text(value);
                    }
                    if (root.TryGetProperty("error_description", out value))
                    {
                        description = Text(value);
                    }
                    if (root.TryGetProperty("errors", out value) && value.ValueKind == JsonValueKind.Object)
                    {
                        errors = new Dictionary<string, IReadOnlyList<string>>();
                        foreach (var field in value.EnumerateObject())
                        {
                            var messages = new List<string>();
                            if (field.Value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var message in field.Value.EnumerateArray())
                                {
                                    messages.Add(Text(message));
                                }
                            }
                            else
                            {
                                messages.Add(Text(field.Value));
                            }
                            errors[field.Name] = messages;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON.
            }
        }

        private static string Text(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return value.GetString();
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                default:
                    return value.GetRawText();
            }
        }
    }
}
