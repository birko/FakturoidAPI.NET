# FakturoidAPI.NET - instructions for Copilot

.NET client for the Fakturoid API v3. Source of truth: the API documentation (https://www.fakturoid.cz/api/v3,
one page per resource, and its changelog). Read the page; never guess attribute names.

## Layout
- `FakturoidAPI.NET/` - the library as a shared project; every new `.cs` file must be added to `FakturoidAPI.NET.projitems`.
  - `Request/` - models sent to the API (plus list filters), `Response/` - models read from it, `ValueLists/Constants.cs` - value lists.
  - `Invoices.cs`, `Subjects.cs`, ... - endpoint methods; `Fakturoid.cs` - HTTP pipeline (`Execute`), `Authentication/` - OAuth tokens,
    `Exceptions/` - typed errors and the status -> exception mapping.
- `Fakturoid/` - NuGet package project (netstandard2.0). `Fakturoid.Tests/` - xUnit tests.

## Conventions
- The shared project keeps to C# 7.3 (`Fakturoid.csproj` sets `LangVersion` 7.3): it is imported into other projects as source.
- Every public async method ends with `CancellationToken cancellationToken = default` and passes it on; `CancellationTest.EveryPublicAsyncMethodTakesACancellationToken` checks it. A cancellation is never wrapped in a Fakturoid exception.
- Every HTTP request goes through `Fakturoid.Execute`. Never log or put into exceptions: headers, the client secret, tokens, the webhook `auth_header`.
- Request properties are nullable and default to `null`; `null` is not sent, so Edit changes only what was set. Never add defaults.
- JSON names exactly as in the API (`[JsonPropertyName("snake_case")]`). `Date` attributes use `Json.DateConverter` (YYYY-MM-DD), `DateTime` attributes are `DateTimeOffset?`, decimals are `decimal?` (Fakturoid sends them as strings).
- Response models inherit request models; a read-only attribute goes to the response model, which also has `[JsonExtensionData]`.
- Value lists are string constants, not enums.
- Do not retry or sleep inside the library (except the single 401 token renewal); throw typed exceptions and let the caller decide.

## Tests
- Every change gets an offline test first: serialization against the JSON example of the documentation (copy it into `Fakturoid.Tests/TestData`), endpoint behaviour against `StubHandler`. `dotnet test Fakturoid.slnx`
- Live tests (`LiveFact`, `LiveWriteFact`) are skipped unless `FAKTUROID_LIVE=1`; credentials come from `~/.fakturoid-test.env`, never from the repository.
- Build with zero warnings: `dotnet build Fakturoid.slnx -c Release`.
