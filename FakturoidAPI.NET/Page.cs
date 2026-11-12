using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Birko.Fakturoid
{
    // One page of a list endpoint (40 records per page, Fakturoid.PageSize).
    public class Page<T>
    {
        public Page(int number, IReadOnlyList<T> items)
        {
            Number = number;
            Items = items;
        }

        // Page number, from 1.
        public int Number { get; }

        public IReadOnlyList<T> Items { get; }

        // Fakturoid sends no total count. A full page may be followed by more; the list ends with an empty page.
        public bool MayHaveMore => Items.Count >= Fakturoid.PageSize;
    }

    // Query string of list endpoints; values are URL-encoded ("+" of a time zone offset becomes %2B).
    internal sealed class Query
    {
        private readonly List<string> parameters = new List<string>();

        public Query Add(string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                parameters.Add(Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(value));
            }
            return this;
        }

        public Query Add(string name, int? value)
        {
            return value == null ? this : Add(name, value.Value.ToString(CultureInfo.InvariantCulture));
        }

        // ISO 8601 with offset, e.g. 2023-08-25T10:55:14+02:00.
        public Query Add(string name, DateTimeOffset? value)
        {
            return value == null ? this : Add(name, value.Value.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
        }

        // Rails array parameter: name[]=a&name[]=b.
        public Query Add(string name, IEnumerable<string> values)
        {
            if (values != null)
            {
                foreach (var value in values.Where(v => !string.IsNullOrEmpty(v)))
                {
                    Add(name + "[]", value);
                }
            }
            return this;
        }

        // Page 1 is the default and is not sent.
        public Query Page(int page)
        {
            return page > 1 ? Add("page", page) : this;
        }

        public string ToString(string path)
        {
            return parameters.Count == 0 ? path : path + "?" + string.Join("&", parameters);
        }
    }
}
