# 06-yarp-fallback progress

## Completed

- Added a startup-validated `LegacyProxy` configuration contract. The configured origin
  must be a root HTTP(S) origin and cannot match an application listener, including
  loopback/wildcard equivalents.
- Added a lowest-priority anonymous catch-all endpoint after the local endpoint mappings,
  so existing and future ASP.NET Core routes remain authoritative.
- Added a dedicated streaming YARP direct forwarder using HTTP/1.1 without redirect,
  cookie-container, decompression, system-proxy, response-buffering, or application retry
  behavior.
- Added a transformer that preserves the incoming raw escaped target, method, query, body,
  end-to-end request/response headers, status, and response stream. It replaces untrusted
  forwarding headers with values from the already trust-processed public request, keeps the
  public `Host`, removes `X-Forwarded-Prefix`, and rewrites legacy-origin redirects to the
  public origin.
- Raised the configured request limit from Kestrel's 30 MB default to the legacy Gallery's
  262,144,000-byte limit and set a 120-second activity timeout, exceeding the legacy
  ASP.NET Framework default of 110 seconds.
- Added in-process fake-origin coverage for route precedence/root fallback, GET, HEAD,
  streaming multipart POST, configured request limits, streaming responses, raw encoded paths and
  queries, ranges and conditional requests, download/cache headers, cookies and
  `Set-Cookie`, redirects, error responses, origin-header scrubbing, forwarded headers,
  cancellation, activity timeout, no POST retry, invalid-origin rejection, and loop
  rejection.

## Code-review follow-up

- Applied the configured compatibility limit to Kestrel and managed `IISServerOptions`,
  and added a checked-in Web SDK `web.config` with native IIS `requestFiltering`. The
  project copies the same artifact to build/publish output.
- Added required `LegacyProxy:PublicOrigins` validation. Production now fails closed while
  the collection is empty; development supplies its two launch origins. Startup rejects
  destinations matching either an internal listener or a public origin, including the
  HTTP/80 to HTTPS/443 TLS-termination topology.
- Replaced canonicalizing `Uri` creation with .NET 10's supported
  `UriCreationOptions.DangerousDisablePathAndQueryCanonicalization` mechanism after
  validating the raw origin-form target. Unsafe/non-lossless targets return 400.
- Added real TCP client → Kestrel proxy → `SocketsHttpHandler` → Kestrel origin tests that
  verify the destination's `IHttpRequestFeature.RawTarget` for `%41`, `%2F`, literal dot
  segments, and encoded dot segments. This supplements (rather than relies upon) TestServer
  `RequestUri.OriginalString` coverage.

## Second code-review follow-up

- Moved raw-target extraction and rejection into `LegacyProxyForwarder`, before
  `IHttpForwarder.SendAsync`. A real Kestrel pipeline test sends malformed `%GG`, receives
  400, and verifies the Kestrel legacy origin handled zero requests.
- Made request-size configuration coherent across hosting models: native IIS Request
  Filtering is set to its unsigned 32-bit maximum (4,294,967,295 bytes), while the
  configured `MaximumRequestBodySize` is enforced identically by Kestrel and
  `IISServerOptions`.
- Constrained configured limits to 262,144,000..4,294,967,295 bytes. Unlimited (`-1`) is
  explicitly unsupported because native IIS cannot represent it. Tests cover a 500,000,000
  byte configured limit, unlimited rejection, native-IIS overflow rejection, managed
  option values, and the published native artifact.

## Files changed

- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/06-yarp-fallback/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/06-yarp-fallback/progress-details.md`
- `src/NuGetGallery.net10/Program.cs`
- `src/NuGetGallery.net10/LegacyProxyForwarder.cs`
- `src/NuGetGallery.net10/LegacyProxyHttpClient.cs`
- `src/NuGetGallery.net10/LegacyProxyOptions.cs`
- `src/NuGetGallery.net10/LegacyProxyTransformer.cs`
- `src/NuGetGallery.net10/appsettings.json`
- `src/NuGetGallery.net10/appsettings.Development.json`
- `src/NuGetGallery.net10/NuGetGallery.net10.csproj`
- `src/NuGetGallery.net10/web.config`
- `tests/NuGetGallery.net10.Facts/LegacyProxyFacts.cs`
- `tests/NuGetGallery.net10.Facts/ProgramFacts.cs`

## Validation

- `dotnet build src\NuGetGallery.net10\NuGetGallery.net10.csproj --configuration Debug --nologo`
  - Succeeded.
- `dotnet test tests\NuGetGallery.net10.Facts\NuGetGallery.net10.Facts.csproj --configuration Release --nologo`
  - Final second-review result: 33 passed, 0 failed, 0 skipped.
- `dotnet build src\NuGetGallery.net10\NuGetGallery.net10.csproj --configuration Release --nologo --no-restore`
  - Succeeded with 0 errors.
- `dotnet publish src\NuGetGallery.net10\NuGetGallery.net10.csproj --configuration Release --output artifacts\review-publish --nologo`
  - Succeeded; published `web.config` has `maxAllowedContentLength=4294967295` and
    `hostingModel=inprocess`.
- Published-host smoke at `http://127.0.0.1:5199`
  - `/_health`: 200; `/_ready`: 200; `/_local`: 200 with `NuGetGallery.net10`.
- `git diff --check`
  - Passed.

## Warnings

- Validation retains existing repository warnings: `NU1507` for the repository's three
  configured package sources under central package management, and analyzer-version
  warnings because `Microsoft.CodeAnalysis.NetAnalyzers` 8.0.0 is older than the .NET 10
  SDK's bundled analyzers. No new compile or test warning was introduced by this task.
- Production must configure the top-level `AllowedHosts` and
  `ForwardedHeaders` trust/host allow-lists for its actual edge proxy and public host.
