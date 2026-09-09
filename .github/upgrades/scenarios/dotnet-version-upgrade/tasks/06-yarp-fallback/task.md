# 06-yarp-fallback: Configure migration-first YARP routing

Make local ASP.NET Core endpoints authoritative and add a terminal anonymous catch-all proxy route to a validated legacy origin. Preserve method, raw/escaped path, query, body, response status/body, authentication cookie, `Set-Cookie`, redirects, cache, content-disposition, range, and conditional headers. Define trusted Host, scheme, client-IP, `Forwarded`, and `X-Forwarded-*` behavior so public URLs never leak the internal origin, and reject self-referential destinations before serving traffic.

Match or deliberately exceed legacy request-size and timeout limits, stream large package uploads/downloads without buffering, propagate cancellation, avoid unsafe retries for non-idempotent requests, and ensure local health/readiness paths do not accidentally shadow legacy routes. Keep route ownership granular so later controller migrations can replace proxy coverage piecemeal; no controller migration is part of this task.

**Done when**: In-process proxy tests cover local-route precedence, GET/HEAD/POST, multipart and near-limit bodies, streaming, ranges/conditionals, encoded paths, redirects, errors, cookies, cancellation, timeout, forwarded headers, and loop rejection, with every unmatched valid request reaching the fake legacy origin.

## Repository research

- The migration host already exists at `src/NuGetGallery.net10` and uses an explicit
  `Program` class (no top-level statements). Its three reserved local endpoints are
  configuration-driven through `GalleryHostOptions`: `/_local`, `/_health`, and `/_ready`.
  `Program.cs` already runs fail-closed forwarded-header middleware first, shared
  reader-only cookie authentication, authorization, Kestrel TLS hardening, and response
  fingerprint scrubbing.
- The proxy package is already centrally pinned: `Yarp.ReverseProxy` **2.3.0** in
  `Directory.Packages.props`; the host already references it and registers
  `AddHttpForwarder()`. Tests use `Microsoft.AspNetCore.TestHost` **10.0.0**, xUnit
  **2.9.0**, and `Microsoft.NET.Test.Sdk` **17.10.0**. No package change is required.
- The legacy project is configured at `https://localhost` in
  `src/NuGetGallery/NuGetGallery.csproj`. Its `Web.config` permits
  `maxRequestLength="256000"` KiB and IIS
  `maxAllowedContentLength="262144000"` bytes (250 MiB). No application execution timeout
  override was found, so the ASP.NET Framework default is 110 seconds; the proxy will use a
  configurable 120-second idle/activity timeout and a 262,144,000-byte request cap.
- YARP 2.3's direct-forwarder API streams request and response bodies, copies status and
  end-to-end headers, honors `RequestAborted`, defaults to no response buffering when
  `AllowResponseBuffering=false`, and applies an activity timeout. A dedicated
  `HttpMessageInvoker` with redirects, cookies, and decompression disabled preserves
  redirects, `Set-Cookie`, and encoded content and does not add application-level retries.
- The catch-all will be a lowest-order anonymous endpoint after the existing local endpoint
  mappings. This lets literal ASP.NET Core routes win now and lets later migrated endpoints
  take ownership one route at a time without changing proxy configuration.
- A custom transformer is needed to use `IHttpRequestFeature.RawTarget` for the exact
  escaped path/query, replace (rather than trust or append) incoming `Forwarded` and
  `X-Forwarded-*` values from the already trust-validated Core request, retain the public
  `Host`, and rewrite any absolute legacy-origin `Location` header to the public
  scheme/authority.
- Startup options validation will require an absolute HTTP(S) origin with no credentials,
  query, fragment, or non-root base path and reject an origin matching any configured
  application URL/Kestrel endpoint (including loopback/wildcard equivalents on the same
  scheme and port). In-process tests will replace the forwarder's message handler with a
  fake legacy `TestServer`; production continues to use a streaming `SocketsHttpHandler`.

## Review follow-up research

- The 250 MiB compatibility limit must be applied at all three request gates used by the
  supported hosting models: Kestrel, managed `IISServerOptions.MaxRequestBodySize`, and
  native IIS Request Filtering. The ASP.NET Core Web SDK supports a checked-in
  `web.config` as the publish-transform input, so the host will carry
  `system.webServer/security/requestFiltering/requestLimits@maxAllowedContentLength`
  alongside the in-process `aspNetCore` handler. The project will explicitly copy this
  artifact to build and publish output so both tests and deployments validate the same file.
- Listener-only loop detection misses Azure App Service's split topology: the process may
  listen on an internal HTTP port while its externally visible origin is HTTPS. Add required,
  validated `LegacyProxy:PublicOrigins` configuration. Reject a legacy origin matching an
  internal listener, or matching a public origin by equivalent host and port, including the
  standard HTTP 80 ↔ HTTPS 443 TLS-termination pair. Keep a collection because a slot can
  legitimately serve multiple externally visible origins.
- `new Uri(string)` canonicalizes request targets before `SocketsHttpHandler` serializes
  `RequestUri.PathAndQuery`; `OriginalString` in a TestServer-only handler does not prove the
  wire request line. .NET 10's supported mechanism is `UriCreationOptions` with
  `DangerousDisablePathAndQueryCanonicalization=true`. Runtime source confirms HTTP/1.1
  serialization writes that `PathAndQuery` directly. Because the API deliberately skips
  validation, the transformer must first fail closed unless the raw target is an ASCII
  origin-form target with no controls, spaces, fragments, backslashes, or malformed percent
  triplets. Real Kestrel → `SocketsHttpHandler` → Kestrel tests will assert byte-equivalent
  `RawTarget` values for encoded unreserved characters (`%41`), encoded slash (`%2F`), and
  literal/encoded dot segments.

## Second review follow-up research

- YARP determines whether a transform short-circuited by inspecting whether the response is
  already set; assigning status 400 inside `TransformRequestAsync` is not a reliable abort
  contract for this direct-forwarder path. Raw-target extraction and validation therefore
  belongs in `LegacyProxyForwarder.ForwardAsync`, before `IHttpForwarder.SendAsync` is
  called. The transformer will consume the already-validated value only.
- IIS `maxAllowedContentLength` is an unsigned 32-bit byte value. A fixed 250 MiB native
  limit conflicts with larger managed limits, while `-1` cannot mean unlimited under IIS.
  Set native Request Filtering to `uint.MaxValue` (4,294,967,295 bytes), constrain
  `MaximumRequestBodySize` to the inclusive range 262,144,000..4,294,967,295, and apply
  that configured value identically to Kestrel and `IISServerOptions`. Unlimited is
  intentionally unsupported so every declared configuration has the same effective limit
  in direct Kestrel and IIS in-process hosting.

## Affected files

- `src/NuGetGallery.net10/Program.cs`
- `src/NuGetGallery.net10/LegacyProxyOptions.cs` (new)
- `src/NuGetGallery.net10/LegacyProxyForwarder.cs` (new)
- `src/NuGetGallery.net10/LegacyProxyHttpClient.cs` (new)
- `src/NuGetGallery.net10/LegacyProxyTransformer.cs` (new)
- `src/NuGetGallery.net10/appsettings.json`
- `src/NuGetGallery.net10/appsettings.Development.json`
- `src/NuGetGallery.net10/NuGetGallery.net10.csproj`
- `src/NuGetGallery.net10/web.config` (new)
- `tests/NuGetGallery.net10.Facts/ProgramFacts.cs`
- `tests/NuGetGallery.net10.Facts/LegacyProxyFacts.cs` (new)
