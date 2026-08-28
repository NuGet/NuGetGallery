# Plan: Integrate Aspire.Hosting.Testing for local NuGet End-to-End tests

## Problem & goal

Today the NuGet End-to-End (functional) tests run against a gallery that is started
**out-of-process** and orchestrated by PowerShell:

- `tools\Start-AspireHost.ps1` runs `dotnet run --project NuGetGallery.AppHost` in a
  separate process, polls a health URL, and returns the PID.
- `tools\Seed-FunctionalTestData.ps1` runs `GalleryTools.exe seedfunctionaltests`,
  which seeds users/orgs/API keys/a base package and writes
  `tests\NuGetGallery.FunctionalTests\settings.CI.json`.
- `dotnet test` runs `NuGetGallery.FunctionalTests` (net10.0, xUnit + Playwright),
  which read `ConfigurationFilePath` → `settings.CI.json` → `GalleryConfiguration.Instance`.
- `tools\Stop-AspireHost.ps1` tears the host down.

The goal is to boot the existing Aspire AppHost **in-process** using
[`Aspire.Hosting.Testing`](https://aspire.dev/testing/overview/) via a shared xUnit
fixture, so that a single `dotnet test` (or F5) spins up the whole distributed app,
seeds data, and runs the **Playwright-based** Functional tests — a real inner-dev loop.

## Decisions (confirmed)

- **Test scope**: The `Aspire.Hosting.Testing` harness runs **only the Playwright-based
  functional tests**. Filter the Playwright tests from `NuGetGallery.FunctionalTests` by
  adding by scoping them as a test suite. The remaining (non-Playwright) P0/P1/P2/AdminApi HTTP functional tests stay in
  `NuGetGallery.FunctionalTests` and continue to use the existing `Start-AspireHost.ps1` /
  `Stop-AspireHost.ps1` orchestration — **those scripts are NOT retired**.
- **Local + CI**: Add the local inner-loop path and use the same harness in CI for the
  Playwright tests. Existing script-based orchestration remains for the other tests.
- **Harness location**: The existing `tests\NuGetGallery.FunctionalTests` project
  references `Aspire.Hosting.Testing` + `NuGetGallery.AppHost`. Normal-mode Playwright
  tests use a shared xUnit collection fixture; files remain in their existing project.
- **Profile**: Support both `ci-gallery` and `full` via `APPHOST_PROFILE`, defaulting to
  `ci-gallery` (fast: Azurite + DB migrations + Gallery only).

## Current-state facts (verified)

- AppHost: `src\NuGetGallery.AppHost\Program.cs` — `DistributedApplication.CreateBuilder(args)`
  … `builder.Build().Run()`. Reads `APPHOST_PROFILE` (default `full`); `ci-gallery` skips the
  Azure Search + V3 search pipeline branch.
- Gallery endpoint is deterministic: IIS Express `WithHttpEndpoint(port: 80, isProxied:false)`
  plus HTTPS 443, health probe at `http://localhost/api/health-probe`. So the E2E base URL
  stays fixed (`https://localhost`), not a dynamically-assigned Aspire proxy port.
- AppHost is net10.0 and builds net472 dependencies (GalleryTools → NuGetGallery) via a
  `BuildNet472Projects` target that shells out to VS MSBuild through `vswhere` (only when not
  inside VS).
- Functional tests: `GalleryConfiguration` is a **static** type initialized on first access
  from `ConfigurationFilePath`; the fixture must set that env var **before** any test touches it.
- Seeding writes `settings.CI.json` and requires `GalleryTools.exe` (net472) to be built and to
  have `appsettings.Aspire.config` next to it.
- CI job `aspire` in `.github\workflows\nugetgallery-ci.yml` (runs on `windows-2025-vs2026`)
  currently performs: build AppHost → Setup-DevEnvironment → Start-AspireHost → Seed → build tests
  → `dotnet test --filter P0|P1|P2|AdminApi` → Stop-AspireHost.

## Progress log

- Added `Aspire.Hosting.Testing` and the AppHost project reference to
  `NuGetGallery.FunctionalTests`.
- Added Gallery resource health signaling for `/api/health-probe`.
- Added an assembly-lifetime xUnit collection fixture that starts Aspire, waits for the
  Gallery, seeds with `GalleryTools.exe`, sets `ConfigurationFilePath`, and tears down.
- Added the `PlaywrightTests` trait to the 10 minimal-profile browser tests. The
  statistics-service and read-only-mode tests remain on their externally configured paths.
- Added `tests\Scripts\RunGalleryPlaywrightTests.ps1` for local and agent validation.
- Split GitHub Actions and Azure Pipelines into externally hosted non-Playwright tests
  and a clean Aspire-hosted Playwright job.
- Verified repeat Debug startup, seeding, and teardown. Release validation narrowed the
  minimal-profile suite to the 10 tests supported by `ci-gallery`.

## Proposed approach

The implementation uses `AspirePlaywrightFixture` in the existing functional-test
assembly. Run the agent harness from the repository root:

```powershell
.\tools\Setup-DevEnvironment.ps1 # one-time, elevated machine setup
.\tests\Scripts\RunGalleryPlaywrightTests.ps1 -Configuration Release
```

Set `-AppHostProfile full` only when the Azure Search prerequisites are available.
The default `ci-gallery` profile is the required local and CI validation path.
