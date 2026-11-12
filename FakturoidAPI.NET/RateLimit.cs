using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;

namespace Birko.Fakturoid
{
    // Rate limit headers (API introduction > Rate-limiting), e.g.
    //   X-RateLimit-Policy: default;q=400;w=60   (quota q requests per window w seconds)
    //   X-RateLimit: default;r=398;t=55          (r requests remaining, reset in t seconds)
    public sealed class RateLimit
    {
        public const string PolicyHeader = "X-RateLimit-Policy";
        public const string LimitHeader = "X-RateLimit";

        public RateLimit(string policy, int? quota, TimeSpan? window, int? remaining, TimeSpan? resetIn, DateTimeOffset receivedAt)
        {
            Policy = policy;
            Quota = quota;
            Window = window;
            Remaining = remaining;
            ResetIn = resetIn;
            ReceivedAt = receivedAt;
        }

        // Policy name, e.g. "default".
        public string Policy { get; }

        // Maximum requests per Window (q).
        public int? Quota { get; }

        // Window length (w).
        public TimeSpan? Window { get; }

        // Requests remaining in the current window (r).
        public int? Remaining { get; }

        // Time until the quota resets, as of ReceivedAt (t).
        public TimeSpan? ResetIn { get; }

        // When the response with these headers arrived (UTC).
        public DateTimeOffset ReceivedAt { get; }

        // When the quota resets.
        public DateTimeOffset? ResetAt => ResetIn == null ? (DateTimeOffset?)null : ReceivedAt + ResetIn.Value;

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}: {1}/{2} remaining, window {3}s, reset at {4:O}",
                Policy, Remaining, Quota, Window?.TotalSeconds, ResetAt);
        }

        // null when the response has no rate limit header.
        internal static RateLimit FromHeaders(HttpResponseHeaders headers, DateTimeOffset receivedAt)
        {
            var limits = Parse(Values(headers, LimitHeader));
            var policies = Parse(Values(headers, PolicyHeader));
            if (limits.Count == 0 && policies.Count == 0)
            {
                return null;
            }
            // Several policies: report the one closest to its limit.
            var limit = limits.OrderBy(l => Number(l.Value, "r") ?? int.MaxValue).FirstOrDefault();
            var name = limit.Key ?? policies.First().Key;
            Dictionary<string, string> policy;
            policies.TryGetValue(name, out policy);
            var w = Number(policy, "w");
            var t = Number(limit.Value, "t");
            return new RateLimit(name, Number(policy, "q"),
                w == null ? (TimeSpan?)null : TimeSpan.FromSeconds(w.Value),
                Number(limit.Value, "r"),
                t == null ? (TimeSpan?)null : TimeSpan.FromSeconds(t.Value),
                receivedAt);
        }

        private static IEnumerable<string> Values(HttpResponseHeaders headers, string name)
        {
            IEnumerable<string> values;
            return headers != null && headers.TryGetValues(name, out values) ? values : Enumerable.Empty<string>();
        }

        // "default;q=400;w=60, other;q=10;w=1" -> name -> parameters.
        internal static Dictionary<string, Dictionary<string, string>> Parse(IEnumerable<string> values)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in values.SelectMany(v => v.Split(',')))
            {
                var parts = item.Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
                if (parts.Length == 0)
                {
                    continue;
                }
                var name = parts[0].Trim('"');
                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var parameter in parts.Skip(1))
                {
                    var eq = parameter.IndexOf('=');
                    if (eq > 0)
                    {
                        parameters[parameter.Substring(0, eq).Trim()] = parameter.Substring(eq + 1).Trim().Trim('"');
                    }
                }
                result[name] = parameters;
            }
            return result;
        }

        private static int? Number(Dictionary<string, string> parameters, string key)
        {
            string text;
            int value;
            return parameters != null && parameters.TryGetValue(key, out text)
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : (int?)null;
        }
    }
}
