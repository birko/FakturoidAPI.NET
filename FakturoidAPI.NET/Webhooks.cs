using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Birko.Fakturoid
{
    // https://www.fakturoid.cz/api/v3/webhooks. Paid plans only. With client credentials, webhook
    // management must be enabled in the user's Settings > User account (else 403); the credentials see
    // and manage only their own webhooks. Receive deliveries with WebhookReceiver.
    public class Webhooks
    {
        private readonly Fakturoid fakturoid;

        public Webhooks(Fakturoid fakturoid)
        {
            this.fakturoid = fakturoid;
        }

        public Task<Page<Response.Webhook>> List(int page = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.GetPage<Response.Webhook>(Path(page), page, cancellationToken);
        }

        public Task<List<Response.Webhook>> ListAll(int maxPages = int.MaxValue, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.GetAllPages<Response.Webhook>(Path, maxPages, cancellationToken);
        }

        public Task<Response.Webhook> View(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.Get<Response.Webhook>(ItemPath(id), cancellationToken);
        }

        public Task<Response.Webhook> Add(Request.Webhook webhook, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (webhook == null)
            {
                throw new ArgumentNullException(nameof(webhook));
            }
            return fakturoid.Send<Request.Webhook, Response.Webhook>(HttpMethod.Post, fakturoid.AccountPath("webhooks.json"), webhook, cancellationToken);
        }

        // PATCH: only the properties that are set change.
        public Task<Response.Webhook> Edit(int id, Request.Webhook webhook, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (webhook == null)
            {
                throw new ArgumentNullException(nameof(webhook));
            }
            return fakturoid.Send<Request.Webhook, Response.Webhook>(new HttpMethod("PATCH"), ItemPath(id), webhook, cancellationToken);
        }

        public Task Delete(int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.SendWithoutResult(HttpMethod.Delete, ItemPath(id), cancellationToken);
        }

        // Events that failed all delivery attempts (kept 60 days); failedDeliveriesUuid from Response.Webhook.
        public Task<List<Response.FailedWebhookDelivery>> FailedDeliveries(string failedDeliveriesUuid, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(failedDeliveriesUuid))
            {
                throw new ArgumentException("failed_deliveries_uuid is required.", nameof(failedDeliveriesUuid));
            }
            return fakturoid.Get<List<Response.FailedWebhookDelivery>>(
                fakturoid.AccountPath("webhooks/" + Uri.EscapeDataString(failedDeliveriesUuid) + "/failed_deliveries.json"), cancellationToken);
        }

        private string Path(int page)
        {
            return new Query().Page(page).ToString(fakturoid.AccountPath("webhooks.json"));
        }

        private string ItemPath(int id)
        {
            return fakturoid.AccountPath("webhooks/" + id.ToString(CultureInfo.InvariantCulture) + ".json");
        }
    }
}
