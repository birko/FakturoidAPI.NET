using System;

namespace Birko.Fakturoid
{
    // Fakturoid.RequestCompleted: one finished HTTP request, for debugging and monitoring.
    // Never carries headers (Authorization holds the access token or the client secret). Token endpoint
    // bodies are never included; OAuth tokens, the webhook auth_header and the invoice token are shown as
    // "***". Bodies still contain personal data (subjects, addresses, e-mails, IBAN).
    public class RequestCompletedEventArgs : EventArgs
    {
        public RequestCompletedEventArgs(string method, string uri, int? statusCode, TimeSpan duration,
            string requestBody, string responseBody, long? responseLength, Exception exception)
        {
            Method = method;
            Uri = uri;
            StatusCode = statusCode;
            Duration = duration;
            RequestBody = requestBody;
            ResponseBody = responseBody;
            ResponseLength = responseLength;
            Exception = exception;
        }

        public string Method { get; }

        // Relative to the API URL, e.g. "accounts/applecorp/invoices.json?page=2".
        public string Uri { get; }

        // null when no response arrived (network error, timeout, cancellation).
        public int? StatusCode { get; }

        public TimeSpan Duration { get; }

        // Only with IncludeBodiesInEvents: the JSON sent.
        public string RequestBody { get; }

        // Only with IncludeBodiesInEvents; null for binary responses (PDF), see ResponseLength.
        public string ResponseBody { get; }

        // Bytes received, null when no response was read.
        public long? ResponseLength { get; }

        // The exception the call throws, or null when it succeeded: an Exceptions.ApiException for HTTP
        // errors, OperationCanceledException when the caller's CancellationToken cancelled it,
        // TimeoutException for Fakturoid.Timeout, HttpRequestException for network failures.
        public Exception Exception { get; }
    }
}
