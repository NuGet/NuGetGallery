# Gallery Playwright tests

These tests protect user-visible NuGet Gallery behavior through the Aspire-hosted local Gallery. They are the source of truth for browser validation; Playwright MCP is an exploratory and debugging aid, not a replacement for committed tests.

## When to add a browser test

Add or update a Playwright test when a change affects a user workflow, rendered page content, navigation, form behavior, authentication experience, accessibility behavior, client-side interaction, or another observable browser contract.

Prefer a controller, service, view-model, or JavaScript unit test when the behavior can be verified reliably below the browser boundary and there is no meaningful end-to-end user interaction. For a UI change, keep focused lower-level coverage and add one browser scenario for the critical user outcome rather than duplicating every branch end to end.

## Test location and structure

Place tests under the feature-oriented folders in this directory. Derive from `NuGetPageTest` and reuse `SignInAsync`, `UploadPackageAsync`, `GalleryConfiguration`, `UrlHelper`, and existing helpers before adding new setup code.

Tests intended for the standard agent runner must include:

```csharp
[Collection(GalleryTestCollection.Definition)]
public class FeatureTest : NuGetPageTest
{
    [Fact]
    [Priority(0)]
    [Category("PlaywrightTests")]
    public async Task UserAction_ProducesExpectedResult()
    {
        // Arrange

        // Act

        // Assert
    }
}
```

Add `[Category("P0Tests")]`, `P1Tests`, or `P2Tests` only when the scenario should also run in that broader functional-test tier. Use unique package IDs and other generated data so tests do not depend on execution order or shared mutable state.

Statistics-service and read-only-mode tests run through separately configured paths. Preserve the metadata and setup conventions in those folders instead of adding them to the standard agent runner without also updating their execution model.

## Locator and assertion guidance

- Prefer `GetByRole`, `GetByLabel`, accessible names, and visible text because they model how users and assistive technology find controls.
- Use CSS selectors for stable Gallery-specific elements when a semantic locator is unavailable. Avoid selectors tied to DOM depth, generated IDs, or styling-only implementation details.
- Use `Expect(...)` assertions and Playwright's element, URL, response, and load-state waits.
- Do not add `Task.Delay`, thread sleeps, retry loops, or timing assumptions to hide a race.
- Assert the user-visible outcome, not only that an action completed.
- Keep authentication and package setup in shared helpers when more than one test needs the flow.

## AppHost profiles

Use the default `ci-gallery` profile for normal browser validation. It starts the minimal local resources needed by the Gallery.

Use `-AppHostProfile full` only when the scenario requires the Azure Search-backed resources and the machine has those prerequisites. Statistics-service and read-only-mode tests retain their separately configured execution paths.

The local Gallery owns fixed ports 80/443. Do not run this suite concurrently with another functional-test process or `Start-AspireHost.ps1`.

## Agentic validation loop

1. Inspect the product change, existing browser tests, and shared helpers.
2. Identify the smallest critical user behavior that proves the feature.
3. Use Playwright MCP against the local Gallery when accessibility-tree inspection, reproduction, screenshots, or interactive debugging helps.
4. Encode the behavior as a committed C# Playwright test.
5. Run the standard suite.
6. Diagnose failures using the test output, `tests\TestResults\PlaywrightTests.trx`, a headed browser, Playwright Inspector, or the Aspire Dashboard.
7. Fix the implementation or test and rerun.
8. Finish with the standard non-debug suite passing and record the exact result in the pull request.

MCP sessions, screenshots, traces, storage state, and other generated artifacts belong under `tests\TestResults` and must not be committed.

The checked-in VS Code MCP server requires Node.js 18 or later. `tools\Start-PlaywrightMcp.ps1` uses `npx` from `PATH` or the Node.js toolchain bundled with Visual Studio and pins the MCP package version for reproducible startup. Review and trust the workspace MCP configuration before starting it; local MCP servers execute code on the developer machine.

To update Playwright MCP, change the pinned `@playwright/mcp` version in `tools\Start-PlaywrightMcp.ps1`, run the launcher's `-ShowHelp` validation, and repeat the localhost navigation and screenshot smoke test before committing.

## Commands

Standard validation:

```powershell
.\tests\Scripts\RunGalleryPlaywrightTests.ps1 -Configuration Release
```

Visible browser:

```powershell
.\tests\Scripts\RunGalleryPlaywrightTests.ps1 -Configuration Release -Headed
```

Playwright Inspector:

```powershell
.\tests\Scripts\RunGalleryPlaywrightTests.ps1 -Configuration Release -PwDebug
```

Aspire Dashboard:

```powershell
.\tests\Scripts\RunGalleryPlaywrightTests.ps1 -Configuration Release -Dashboard
```

Combine `-Dashboard -PwDebug` for an interactive run that keeps both debugging surfaces available. Always rerun without debugging switches before reporting success.

The script writes `tests\TestResults\PlaywrightTests.trx` and prints failed, passed, skipped, total, and run-duration values when it completes.
