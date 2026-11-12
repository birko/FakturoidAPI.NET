# FakturoidAPI.NET
.NET client library for the [Fakturoid API v3](https://www.fakturoid.cz/api/v3).
> Codes are wrapped as .net Shared Library. So you can [download](https://github.com/birko/FakturoidAPI.NET/releases) them and include into your programs or .dll libraries.

> You can also use the NuGet package `Fakturoid` (not published yet)

Library uses System.Net.Http.HttpClient as communication layer and System.Text.Json for serialization and deserialization.

What it adds over other Fakturoid clients:
* every async method takes a `CancellationToken` and passes it to `HttpClient.SendAsync` and to the response body read; a cancellation is always `OperationCanceledException`, never an API exception
* OAuth 2 Client Credentials with a cached access token, renewed shortly before it expires
* rate limit headers exposed, `429` thrown with the reset time (the library never sleeps on its own)
* typed exceptions for `422`, `402`, `403` (missing bank account, locked document), `404`, `429`, `503`
* PDF download that tells "still generating" (`204`) from a PDF, plus a bounded polling helper

Implementation used by [FinStat.sk](http://www.finstat.sk)

## Repository structure
* **FakturoidAPI.NET/** - the library as a .NET Shared Project (`FakturoidAPI.NET.projitems`), import it into your project. The code keeps to C# 7.3 and needs the `System.Text.Json` package on .NET Framework / netstandard2.0
* **Fakturoid/** - NuGet package project (`Fakturoid`, netstandard2.0)
* **Fakturoid.Tests/** - tests: offline tests (serialization against the JSON examples of the API documentation, a stubbed `HttpMessageHandler` for errors, tokens, paging, PDF and cancellation; no credentials) and opt-in live tests (see [Tests](#tests))
* **Fakturoid.slnx** - solution

## Client structure
The main api client class [`Fakturoid`](#fakturoid) has this hierarchy. Most of used classes is from `Birko.Fakturoid` namespace

### Fakturoid
Main class. Gets the OAuth access token, sends the mandatory `User-Agent`, serializes requests, deserializes responses and maps errors.

```csharp
var fakturoid = new Fakturoid(clientId, clientSecret, accountSlug: "applecorp",
    applicationName: "Symbio", contactEmail: "dev@example.com");
var invoice = await fakturoid.Invoices.View(529, cancellationToken);
```

#### Public Properties
* **AccountSlug** - the account in `https://app.fakturoid.cz/api/v3/accounts/{slug}/...`. Can be `null` until an account endpoint is called; `Users.Current()` lists the accounts of the user
* **UserAgent** - `"applicationName (contactEmail)"`, sent with every request (Fakturoid answers `400` without it)
* **Timeout** - TimeSpan, default 30 s. Time limit of one HTTP request including reading the response; exceeded it throws `TimeoutException`
* **IncludeBodiesInEvents** - boolean, default false. Adds request and response bodies to [`RequestCompleted`](#request-events)
* **RateLimit** - [rate limit](#rate-limits) from the last response, `null` before the first one
* **TokenProvider** - the `OAuthTokenProvider` (see [Authorization](#authorization))
* **Users** - instance of [`Users`](#users) class, description bellow
* **Account** - instance of [`Accounts`](#accounts) class, description bellow
* **Subjects** - instance of [`Subjects`](#subjects) class, description bellow
* **Invoices** - instance of [`Invoices`](#invoices) class, description bellow
* **InvoicePayments** - instance of [`InvoicePayments`](#invoicepayments) class, description bellow
* **Webhooks** - instance of [`Webhooks`](#webhooks) class, description bellow

#### Public Methods
* **Fakturoid(string clientId, string clientSecret, string accountSlug, string applicationName, string contactEmail, HttpClient httpClient = null)** - constructor for the Client Credentials flow. Client id and secret are in Fakturoid under Settings → User account. Without `httpClient` one shared `HttpClient` is used
* **Fakturoid(OAuthTokenProvider tokenProvider, string accountSlug, string applicationName, string contactEmail, HttpClient httpClient = null, string apiUrl = DefaultApiUrl)** - constructor for any OAuth flow

Every async method of every class takes `CancellationToken cancellationToken = default` as its last parameter.

#### Authorization
OAuth 2 Client Credentials (`POST /oauth/token` with HTTP Basic `client_id:client_secret`). The access token lives 2 hours and has no refresh token; `ClientCredentialsTokenProvider` caches it and requests a new one `RefreshBefore` (default 5 minutes) before it expires. Concurrent calls share one token request. When the API answers `401` to a cached token, the token is dropped and the request is repeated once with a new one. `OAuthTokenProvider.Invalidate()` drops the token by hand.

Fakturoid allows the Client Credentials flow for access to your own account. An Authorization Code provider (multi-tenant apps) can be added later as another `OAuthTokenProvider` subclass (`TokenRequestBody`, `OnTokenReceived` for the refresh token) without changing the client.

#### Paging
Lists return 40 records per page (`Fakturoid.PageSize`). `List(..., int page = 1)` returns `Page<T>` with `Number`, `Items` and `MayHaveMore` (a full page). Fakturoid sends no total count; `ListAll(..., int maxPages = int.MaxValue)` reads pages until an empty one.

#### Errors
All in the `Birko.Fakturoid.Exceptions` namespace.
* **ApiException** - Fakturoid answered 4xx/5xx. `StatusCode`, `Method`, `Uri`, `Error` and `ErrorDescription` (`{"error": ..., "error_description": ...}`), `Errors` (field → messages of `{"errors": {...}}`, empty when none), `ResponseBody`, `RateLimit`. Subclasses:
  * **ValidationException** - `422`: invalid data, an invoice or subject that cannot be deleted, an action that cannot be fired
  * **AuthenticationException** - `401`, or `400`/`401` of the token endpoint (wrong client id or secret)
  * **AccountBlockedException** - `402`: the account is blocked for an unpaid Fakturoid invoice; `ResponseBody` lists the unpaid invoices
  * **ForbiddenException** - `403`, e.g. a payment on a paid invoice, a subject over the plan limit, webhooks not enabled
    * **MissingBankAccountException** - `403` on invoice create: the account has no bank account. It cannot be set via the API, set it up in Fakturoid
    * **DocumentLockedException** - `403` `document_locked`: unlock the document first
  * **NotFoundException** - `404`
  * **RateLimitExceededException** - `429`, with `ResetAt` (see [Rate limits](#rate-limits))
  * **ServiceUnavailableException** - `503`: maintenance (read-only mode) or a busy search; Fakturoid does not keep the request, repeat it later
* **ParseException** - a successful response that is not the expected JSON; `ResponseExcerpt` holds its start
* **PdfNotReadyException** - `Invoices.DownloadPdfWhenReady` gave up

Not Fakturoid errors: a cancelled `CancellationToken` throws `OperationCanceledException` (carrying your token), `Timeout` throws `TimeoutException`, a network failure stays `HttpRequestException`. So "my deadline" (`OperationCanceledException`), "no answer" (`TimeoutException`, `HttpRequestException`) and "Fakturoid refused" (`ApiException`) are told apart by type. The library never retries on its own except the single `401` token renewal above.

#### Rate limits
Fakturoid sends `X-RateLimit-Policy: default;q=400;w=60` and `X-RateLimit: default;r=398;t=55`. `Fakturoid.RateLimit` (`RateLimit` class) has `Policy`, `Quota` (q), `Window` (w), `Remaining` (r), `ResetIn` (t), `ReceivedAt` and `ResetAt`. On `429` the library throws `RateLimitExceededException` with `ResetAt` (from `X-RateLimit`, else `Retry-After`) and does not wait: retrying is the caller's decision.

#### Request events
`RequestCompleted` fires after every HTTP request (also the token request, failed and cancelled ones) with `Method`, `Uri` (relative to the API URL), `StatusCode` (`null` without response), `Duration`, `ResponseLength` and `Exception`. `RequestBody` and `ResponseBody` are filled only with `IncludeBodiesInEvents = true`; binary responses (PDF) report only `ResponseLength`.
* Headers are never included (`Authorization` holds the access token or the client secret). Bodies of the token request are never included. `access_token`, `refresh_token`, the webhook `auth_header`, the invoice `token` and `public_html_url` are shown as `***`.
* Bodies still contain **personal data** (subjects, addresses, e-mails, IBAN): use them for debugging, do not send them to central logging in production.
* Exceptions thrown by a handler are ignored, so a failing logger never fails an API call (e.g. after an invoice was already created).
* The event belongs to the `Fakturoid` instance, handlers can be called from any thread.

```csharp
#if DEBUG
fakturoid.IncludeBodiesInEvents = true;
fakturoid.RequestCompleted += (s, e) => Debug.WriteLine($"{e.Method} {e.Uri} -> {e.StatusCode} ({e.Duration.TotalMilliseconds} ms)\n{e.RequestBody}\n{e.ResponseBody}");
#endif
```

#### Request models
Models sent to the API are in `Birko.Fakturoid.Request`, models read from it in `Birko.Fakturoid.Response` (a response model inherits its request model). Request properties left `null` are not sent, so `Edit` methods change only the properties that were set. Response models keep attributes this library does not know yet in `ExtensionData`. Value lists (statuses, document types, actions, webhook events, ...) are string constants in `Birko.Fakturoid.ValueLists`.

### Users
Class that wrappes users API calls
#### Public Methods
* **Current()** - the user of the credentials, with `Accounts` (their `Slug` is the `AccountSlug`). Needs no `AccountSlug`
* **List()** - users of the account

### Accounts
Class that wrappes account API calls, available as `Fakturoid.Account`
#### Public Methods
* **View()** - account detail (read-only; plan, `ApiCallsLimit`, `ApiCallsUsed`, defaults, ...)

### Subjects
Class that wrappes subjects (customers and suppliers) API calls
#### Public Methods
* **List(Request.SubjectFilter filter = null, int page = 1)** - one page of subjects; filter by `Since`, `UpdatedSince`, `CustomId`
* **ListAll(Request.SubjectFilter filter = null, int maxPages = int.MaxValue)** - all pages
* **FindByCustomId(string customId)** - the subject with your `custom_id`, or `null` (first match; Fakturoid does not promise uniqueness)
* **Search(string query, int page = 1)** - fulltext in name, full name, e-mails, registration and VAT number, private note
* **View(int id)** - subject detail
* **Add(Request.Subject subject)** - creates a subject (`Name` is required); `ForbiddenException` when the plan's subject limit is reached
* **Edit(int id, Request.Subject subject)** - updates the properties that are set
* **Delete(int id)** - deletes a subject; `ValidationException` when it has documents

### Invoices
Class that wrappes invoice API calls (invoices, proformas, corrections, tax documents)
#### Public Methods
* **List(Request.InvoiceFilter filter = null, int page = 1)** - one page; filter by `Since`, `Until`, `UpdatedSince`, `UpdatedUntil`, `SubjectId`, `CustomId`, `Number`, `Status`, `DocumentType`
* **ListAll(Request.InvoiceFilter filter = null, int maxPages = int.MaxValue)** - all pages
* **FindByCustomId(string customId)** - all invoices with your `custom_id` (a list: Fakturoid does not make it unique)
* **Search(string query, IEnumerable&lt;string&gt; tags = null, int page = 1)** - fulltext in number, variable symbol, client name, notes and lines; `tags` searches tags
* **View(int id)** - invoice detail, including `Payments`
* **Add(Request.Invoice invoice)** - creates an invoice (`SubjectId` is required). `MissingBankAccountException` when the account has no bank account, `ValidationException` for invalid data
* **Edit(int id, Request.Invoice invoice)** - updates the properties that are set. Lines with `Id` are updated, lines without `Id` are added, `Destroy = true` deletes a line. `DocumentLockedException` for a locked invoice
* **Delete(int id)** - deletes an invoice; `ValidationException` when it cannot be deleted (locked, has a correction, paid proforma, ...)
* **Fire(int id, string action)** - fires an action (`ValueLists.InvoiceAction`); `ValidationException` when it cannot be fired. Shortcuts: **MarkAsSent**, **Cancel**, **UndoCancel**, **Lock**, **Unlock**, **MarkAsUncollectible**, **UndoUncollectible** (each `(int id)`)
* **DownloadPdf(int id)** - one attempt: `PdfDownload` with `IsReady = false` while Fakturoid is still generating the PDF (`204`), else `Content` with the PDF
* **DownloadPdfWhenReady(int id, int maxAttempts = 10, TimeSpan? delay = null)** - polls `DownloadPdf` up to `maxAttempts` times, `delay` (default 2 s) apart; throws `PdfNotReadyException` after the last one. The `CancellationToken` also cuts the waiting short. Fakturoid advises to wait a second or two after creating or changing an invoice

#### Duplicate invoices
`POST invoices.json` is not idempotent and Fakturoid does **not** promise that `custom_id` prevents a duplicate. When the answer to `Add` is lost (timeout, cancellation, network error), look the invoice up with `FindByCustomId` before sending `Add` again:
```csharp
// Re-sending an invoice whose earlier Add got no answer:
var existing = await fakturoid.Invoices.FindByCustomId(request.CustomId, cancellationToken);
var invoice = existing.Count > 0 ? existing[0] : await fakturoid.Invoices.Add(request, cancellationToken);
```

### InvoicePayments
Class that wrappes invoice payments API calls. Payments are read with the invoice (`Invoices.View(id).Payments`)
#### Public Methods
* **Add(int invoiceId, Request.InvoicePayment payment = null)** - adds a payment; without values it pays the remaining amount today. `ForbiddenException` when the invoice is already paid
* **CreateTaxDocument(int invoiceId, int paymentId)** - issues a tax document for the payment (`TaxDocumentId`)
* **Delete(int invoiceId, int paymentId)** - deletes a payment

### Webhooks
Class that wrappes webhooks API calls. Webhooks are available in paid plans; with client credentials, webhook management must be enabled in Settings → User account, and the credentials see only their own webhooks
#### Public Methods
* **List(int page = 1)**, **ListAll(int maxPages = int.MaxValue)** - webhooks
* **View(int id)** - webhook detail
* **Add(Request.Webhook webhook)** - creates a webhook (`WebhookUrl`, `AuthHeader`, `Events` from `ValueLists.WebhookEvent`)
* **Edit(int id, Request.Webhook webhook)** - updates the properties that are set (e.g. `Active = false`)
* **Delete(int id)** - deletes a webhook
* **FailedDeliveries(string failedDeliveriesUuid)** - events that failed all delivery attempts (kept 60 days)

#### Webhook receiver
`Birko.Fakturoid.WebhookReceiver` helps the receiver of the deliveries. Fakturoid does not sign them: it sends the webhook's `auth_header` as the `Authorization` header and an `Idempotency-Key` (same across retries).
* **VerifyAuthorization(string receivedAuthorizationHeader, string expectedAuthHeader)** - constant-time comparison; an empty expected value throws (a webhook without `auth_header` cannot be verified)
* **Parse(string body)** (or `byte[] body`) - reads the body as `WebhookPayload` (`WebhookId`, `EventName`, `CreatedAt`, `Body`), with `Invoice`, `Payment`, `Payments`, `Subject` and `GetBody<T>(name)` for the resources of the event
* Header names as constants (`AuthorizationHeader`, `IdempotencyKeyHeader`)

Answer 2xx within 30 seconds, otherwise Fakturoid retries up to 5 times; deduplicate by the `Idempotency-Key` and answer 2xx also for a duplicate.
```csharp
if (!WebhookReceiver.VerifyAuthorization(request.Headers[WebhookReceiver.AuthorizationHeader], webhookAuthHeader))
{
    return Unauthorized();
}
var webhook = WebhookReceiver.Parse(await ReadRawBodyAsync(request));
if (webhook.EventName == ValueLists.WebhookEvent.InvoicePaid) { var paid = webhook.Invoice; /* ... */ }
```

## Tests
* Offline (no credentials): `dotnet test Fakturoid.slnx`
* Live tests are skipped unless `FAKTUROID_LIVE=1`. They read the credentials from `~/.fakturoid-test.env` (or the path in `FAKTUROID_TEST_ENV`), never from the repository:
  ```
  FAKTUROID_CLIENT_ID=...
  FAKTUROID_CLIENT_SECRET=...
  FAKTUROID_ACCOUNT_SLUG=...
  FAKTUROID_CONTACT_EMAIL=...
  ```
  Fakturoid has no sandbox. The read-only tests run with `FAKTUROID_LIVE=1`; the test that creates a subject, an invoice and a payment (and deletes them again) also needs `FAKTUROID_LIVE_WRITE=1`. Use a separate test account for it, never the production one.

## Not covered yet
Invoice Messages, Bank Accounts, Number Formats, Events, Todos, Expenses, Expense Payments, Inbox Files, Inventory Items, Inventory Moves, Generators, Recurring Generators, invoice attachment download, the Authorization Code flow.
