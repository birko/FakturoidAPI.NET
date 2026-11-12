using Birko.Fakturoid.Request;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Birko.Fakturoid
{
    // https://www.fakturoid.cz/api/v3/subjects (customers and suppliers).
    public class Subjects
    {
        private readonly Fakturoid fakturoid;

        public Subjects(Fakturoid fakturoid)
        {
            this.fakturoid = fakturoid;
        }

        // One page (40 records) of subjects matching the filter.
        public Task<Page<Response.Subject>> List(SubjectFilter filter = null, int page = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.GetPage<Response.Subject>(Path(filter, page), page, cancellationToken);
        }

        // All pages, until an empty page or maxPages.
        public Task<List<Response.Subject>> ListAll(SubjectFilter filter = null, int maxPages = int.MaxValue, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.GetAllPages<Response.Subject>(page => Path(filter, page), maxPages, cancellationToken);
        }

        // The subject with this custom_id, or null. Fakturoid does not promise custom_id is unique: with
        // several matches the first (as listed) is returned; use ListAll with SubjectFilter.CustomId to see all.
        public async Task<Response.Subject> FindByCustomId(string customId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(customId))
            {
                throw new ArgumentException("custom_id is required.", nameof(customId));
            }
            var page = await List(new SubjectFilter { CustomId = customId }, 1, cancellationToken).ConfigureAwait(false);
            return page.Items.FirstOrDefault();
        }

        // Fulltext in name, full_name, email, email_copy, registration_no, vat_no and private_note.
        public Task<Page<Response.Subject>> Search(string query, int page = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var path = new Query().Add("query", query).Page(page).ToString(fakturoid.AccountPath("subjects/search.json"));
            return fakturoid.GetPage<Response.Subject>(path, page, cancellationToken);
        }

        public Task<Response.Subject> View(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.Get<Response.Subject>(ItemPath(id), cancellationToken);
        }

        // Name is required. 403 (ForbiddenException) when the plan's subject limit would be exceeded.
        public Task<Response.Subject> Add(Request.Subject subject, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }
            return fakturoid.Send<Request.Subject, Response.Subject>(HttpMethod.Post, fakturoid.AccountPath("subjects.json"), subject, cancellationToken);
        }

        // PATCH: only the properties that are set change.
        public Task<Response.Subject> Edit(int id, Request.Subject subject, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }
            return fakturoid.Send<Request.Subject, Response.Subject>(new HttpMethod("PATCH"), ItemPath(id), subject, cancellationToken);
        }

        // 422 (ValidationException) when the subject has documents.
        public Task Delete(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.SendWithoutResult(HttpMethod.Delete, ItemPath(id), cancellationToken);
        }

        private string Path(SubjectFilter filter, int page)
        {
            return (filter ?? new SubjectFilter()).ToPath(fakturoid.AccountPath("subjects.json"), page);
        }

        private string ItemPath(int id)
        {
            return fakturoid.AccountPath("subjects/" + id.ToString(CultureInfo.InvariantCulture) + ".json");
        }
    }
}
