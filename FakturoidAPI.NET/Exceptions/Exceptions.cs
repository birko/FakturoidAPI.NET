using System;
using System.Collections.Generic;
using System.Linq;

namespace Birko.Fakturoid.Exceptions
{
    // Base of all exceptions the library throws about a Fakturoid response. A cancelled CancellationToken
    // is never one of them (OperationCanceledException), the request Timeout is TimeoutException and a
    // network failure stays HttpRequestException.
    public abstract class FakturoidException : Exception
    {
        protected FakturoidException(string message, Exception inner, int? statusCode, string method, string uri)
            : base(message, inner)
        {
            StatusCode = statusCode;
            Method = method;
            Uri = uri;
        }

        public int? StatusCode { get; }
        public string Method { get; }
        // Relative to the API URL, e.g. "accounts/applecorp/invoices.json".
        public string Uri { get; }
    }

    // Fakturoid answered with an HTTP error (4xx/5xx). Fakturoid uses two error bodies:
    // {"errors": {"field": ["message"]}} (always for 422) and {"error": "code", "error_description": "text"}.
    public class ApiException : FakturoidException
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoErrors = new Dictionary<string, IReadOnlyList<string>>();

        public ApiException(int statusCode, string method, string uri, string error, string errorDescription,
            IReadOnlyDictionary<string, IReadOnlyList<string>> errors, string responseBody, RateLimit rateLimit)
            : base(BuildMessage(statusCode, method, uri, error, errorDescription, errors), null, statusCode, method, uri)
        {
            Error = error;
            ErrorDescription = errorDescription;
            Errors = errors ?? NoErrors;
            ResponseBody = responseBody;
            RateLimit = rateLimit;
        }

        // String code of the error, e.g. "invalid_request", "document_locked"; null for the "errors" format.
        public string Error { get; }

        public string ErrorDescription { get; }

        // Field errors (422 and some 403): field name -> messages. Empty, never null.
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors { get; }

        // The raw response body (secrets hidden), e.g. the list of unpaid invoices of a 402. It may contain
        // personal data: do not send it to central logging.
        public string ResponseBody { get; }

        // Rate limit headers of this response, null when it had none.
        public RateLimit RateLimit { get; }

        private static string BuildMessage(int statusCode, string method, string uri, string error, string errorDescription,
            IReadOnlyDictionary<string, IReadOnlyList<string>> errors)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(error))
            {
                parts.Add(error);
            }
            if (!string.IsNullOrEmpty(errorDescription))
            {
                parts.Add(errorDescription);
            }
            if (errors != null && errors.Count > 0)
            {
                parts.Add(string.Join("; ", errors.Select(e => e.Key + ": " + string.Join(", ", e.Value))));
            }
            return string.Format("Fakturoid answered {0} {1} with {2}{3}", method, uri, statusCode,
                parts.Count > 0 ? ": " + string.Join(" - ", parts) : string.Empty);
        }
    }

    // 400 or 401 of the token endpoint (wrong client id/secret) or 401 of the API.
    public class AuthenticationException : ApiException
    {
        public AuthenticationException(int statusCode, string method, string uri, string error, string errorDescription,
            IReadOnlyDictionary<string, IReadOnlyList<string>> errors, string responseBody, RateLimit rateLimit)
            : base(statusCode, method, uri, error, errorDescription, errors, responseBody, rateLimit)
        {
        }
    }

    // 402: the Fakturoid account is blocked because of an overdue invoice from Fakturoid;
    // ResponseBody holds the list of the unpaid invoices.
    public class AccountBlockedException : ApiException
    {
        public AccountBlockedException(int statusCode, string method, string uri, string error, string errorDescription,
            IReadOnlyDictionary<string, IReadOnlyList<string>> errors, string responseBody, RateLimit rateLimit)
            : base(statusCode, method, uri, error, errorDescription, errors, responseBody, rateLimit)
        {
        }
    }

    // 403: the action is not allowed (e.g. a payment on a paid invoice, a subject over the plan limit,
    // webhooks not enabled for the credentials).
    public class ForbiddenException : ApiException
    {
        public ForbiddenException(int statusCode, string method, string uri, string error, string errorDescription,
            IReadOnlyDictionary<string, IReadOnlyList<string>> errors, string responseBody, RateLimit rateLimit)
            : base(statusCode, method, uri, error, errorDescription, errors, responseBody, rateLimit)
        {
        }
    }

    // 403 on invoice create: the account has no bank account. It cannot be set via the API; set it up
    // in Fakturoid (Settings > Bank accounts). Errors["bank_account"] holds the message with the link.
    public class MissingBankAccountException : ForbiddenException
    {
        public MissingBankAccountException(int statusCode, string method, string uri, string error, string errorDescription,
            IReadOnlyDictionary<string, IReadOnlyList<string>> errors, string responseBody, RateLimit rateLimit)
            : base(statusCode, method, uri, error, errorDescription, errors, responseBody, rateLimit)
        {
        }
    }

    // 403 "document_locked": the document must be unlocked (Invoices.Unlock) first.
    public class DocumentLockedException : ForbiddenException
    {
        public DocumentLockedException(int statusCode, string method, string uri, string error, string errorDescription,
            IReadOnlyDictionary<string, IReadOnlyList<string>> errors, string responseBody, RateLimit rateLimit)
            : base(statusCode, method, uri, error, errorDescription, errors, responseBody, rateLimit)
        {
        }
    }

    // 404.
    public class NotFoundException : ApiException
    {
        public NotFoundException(int statusCode, string method, string uri, string error, string errorDescription,
            IReadOnlyDictionary<string, IReadOnlyList<string>> errors, string responseBody, RateLimit rateLimit)
            : base(statusCode, method, uri, error, errorDescription, errors, responseBody, rateLimit)
        {
        }
    }

    // 422: invalid data, see Errors (field -> messages); also "cannot be deleted" and "event cannot be fired".
    public class ValidationException : ApiException
    {
        public ValidationException(int statusCode, string method, string uri, string error, string errorDescription,
            IReadOnlyDictionary<string, IReadOnlyList<string>> errors, string responseBody, RateLimit rateLimit)
            : base(statusCode, method, uri, error, errorDescription, errors, responseBody, rateLimit)
        {
        }
    }

    // 429: the rate limit is used up. The library does not wait and retry; repeat after ResetAt.
    public class RateLimitExceededException : ApiException
    {
        public RateLimitExceededException(int statusCode, string method, string uri, string error, string errorDescription,
            IReadOnlyDictionary<string, IReadOnlyList<string>> errors, string responseBody, RateLimit rateLimit,
            DateTimeOffset? resetAt)
            : base(statusCode, method, uri, error, errorDescription, errors, responseBody, rateLimit)
        {
            ResetAt = resetAt;
        }

        // When the quota resets (X-RateLimit t=, else Retry-After); null when the response said neither.
        public DateTimeOffset? ResetAt { get; }
    }

    // 503: maintenance (read-only mode) or a busy search endpoint; Fakturoid does not keep the request, retry later.
    public class ServiceUnavailableException : ApiException
    {
        public ServiceUnavailableException(int statusCode, string method, string uri, string error, string errorDescription,
            IReadOnlyDictionary<string, IReadOnlyList<string>> errors, string responseBody, RateLimit rateLimit)
            : base(statusCode, method, uri, error, errorDescription, errors, responseBody, rateLimit)
        {
        }
    }

    // A successful response that is not what was expected (not JSON, wrong shape, empty).
    public class ParseException : FakturoidException
    {
        public ParseException(string message, Exception inner, int? statusCode, string method, string uri, string responseExcerpt)
            : base(message, inner, statusCode, method, uri)
        {
            ResponseExcerpt = responseExcerpt;
        }

        // The start of the response body (secrets hidden), null for token responses.
        public string ResponseExcerpt { get; }
    }

    // Invoices.DownloadPdfWhenReady: the PDF was still being generated after all attempts.
    public class PdfNotReadyException : FakturoidException
    {
        public PdfNotReadyException(string message, string method, string uri, int attempts)
            : base(message, null, 204, method, uri)
        {
            Attempts = attempts;
        }

        public int Attempts { get; }
    }
}
