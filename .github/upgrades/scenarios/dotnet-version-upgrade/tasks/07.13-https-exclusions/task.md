# 07.13-https-exclusions: Implement HTTPS redirection exclusions for health endpoints

## Objective
Ensure health check and monitoring endpoints remain accessible over plain HTTP while forcing HTTPS for all other traffic.

## Background
The original OWIN implementation had `Gallery.ForceSslExclusion` configuration (e.g., "/api/health-probe;/api/status") that excluded certain paths from SSL redirection. These endpoints are called by:
- Load balancers (may not support HTTPS health checks)
- Internal monitoring systems
- Kubernetes liveness/readiness probes

ASP.NET Core's built-in `UseHttpsRedirection()` redirects ALL HTTP traffic with no exclusion mechanism.

## Scope
Implement one of these approaches:

**Option A: Custom middleware before UseHttpsRedirection**
- Create simple middleware that short-circuits for excluded paths
- Check path against Gallery:ForceSslExclusion configuration
- If excluded, continue pipeline without redirect
- If not excluded, let UseHttpsRedirection handle it

**Option B: Endpoint-specific configuration**
- Configure health endpoints with custom metadata
- Use endpoint routing to serve these over HTTP only
- May require separate endpoint setup

**Option C: Restore ForceSslMiddleware**
- Restore deleted custom middleware with exclusion logic
- More explicit control over behavior

## Recommended Approach
Option A - lightweight middleware that checks exclusion list before UseHttpsRedirection.

## Configuration
Use existing `Gallery:ForceSslExclusion` from appsettings.json:
```json
"Gallery": {
  "ForceSslExclusion": "/api/health-probe;/api/status"
}
```

## Done When
- Health probe endpoints accessible over HTTP
- All other endpoints redirect to HTTPS
- Configuration honors Gallery:ForceSslExclusion setting
- Validation: curl http://site/api/health-probe returns 200 (no redirect)
- Validation: curl http://site/ returns 301/302 redirect to https://

## Dependencies
- Blocked on: 07.08 (need endpoints implemented to test)
