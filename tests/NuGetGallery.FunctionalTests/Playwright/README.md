# Gallery Playwright tests

This README is the authoritative contract for Gallery browser-test helpers, structure, locators, execution, and reporting. These committed C# tests protect user-visible NuGet Gallery behavior through the Aspire-hosted local Gallery. Playwright MCP is an optional exploratory and debugging aid, not a replacement for committed tests or the standard runner.

## When to add a browser test

Add or update a Playwright test when a change affects a user workflow, rendered page content, navigation, form behavior, authentication experience, accessibility behavior, client-side interaction, or another observable browser contract.

Prefer a controller, service, view-model, or JavaScript unit test when the behavior can be verified reliably below the browser boundary and there is no meaningful end-to-end user interaction. For a UI change, keep focused lower-level coverage and add one browser scenario for the critical user outcome rather than duplicating every branch end to end.

## Test location and structure

Place tests under the feature-oriented folders in this directory. Derive from `NuGetPageTest` and reuse `SignInAsync`, `UploadPackageAsync`, `GalleryConfiguration`, `UrlHelper`, and existing helpers before adding new setup code.

Tests intended for the standard runner must include:

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

The harness is Windows-only; see the [repository prerequisites](../../../.github/copilot-instructions.md#prerequisites) before running it. The suite fixture starts the host, seeds data, and tears it down after the tests; do not assume the Gallery remains available for browser exploration after the runner exits.

Use `-AppHostProfile full` only when the scenario requires the Azure Search-backed resources and the machine has those prerequisites. Statistics-service and read-only-mode tests retain their separately configured execution paths.

The local Gallery owns fixed ports 80/443. Do not run this suite concurrently with another functional-test process or `Start-AspireHost.ps1`.

## Validation workflow

Follow this workflow in the agent already implementing the feature; no separate specialist agent is required.

1. Inspect the product change, existing browser tests, and shared helpers before editing.
2. Identify the smallest critical user behavior that proves the feature.
3. Use Playwright MCP against the local Gallery when accessibility-tree inspection, reproduction, screenshots, or interactive debugging helps.
4. Encode the behavior as a committed C# Playwright test.
5. Run the standard suite.
6. Diagnose failures using the test output, `tests\TestResults\PlaywrightTests.trx`, a headed browser, Playwright Inspector, or the Aspire Dashboard.
7. Fix the implementation or test and rerun.
8. Finish with the standard non-debug suite passing and report the evidence below.

### Reporting

Report the product change, the committed test added or updated, the exact validation command, and passed/failed/skipped counts from the run. Include useful diagnostic observations separately; MCP or manual browser activity is not automated validation. If prerequisites or the harness prevent execution, state the blocker and what was not run instead of claiming success.

Never commit credentials. MCP sessions, screenshots, traces, browser storage state, and other generated artifacts belong under `tests\TestResults` and must not be committed.

## Optional browser tooling

Use Playwright MCP when it is already available and materially helps with accessibility-tree inspection, reproduction, screenshots, or interactive diagnosis. If it is unavailable, continue implementing and running the committed C# tests without it. Neither this workflow nor the skill automatically installs tools or changes client configuration.

For opt-in setup, follow the documentation for your client:

- [Copilot CLI MCP setup](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-mcp-servers).
- [VS Code MCP setup](https://code.visualstudio.com/docs/agent-customization/mcp-servers).
- [Playwright MCP setup for other clients](https://github.com/microsoft/playwright-mcp#getting-started).

Client configuration formats and paths differ; do not assume a VS Code workspace configuration configures another client or an unattended cloud run. Review the server publisher, launch command, permissions, and configuration before starting it: local MCP servers execute code on your machine. Browser tooling needs a running Gallery reachable from the browser; coordinate exploration with the host lifecycle and fixed-port restriction above rather than starting a competing host.

### Existing Windows/VS Code example

The checked-in [VS Code MCP configuration](../../../.vscode/mcp.json) and [launcher](../../../tools/Start-PlaywrightMcp.ps1) are optional examples, not required setup. The launcher requires Node.js 18 or later, uses `npx` from `PATH` or the Node.js toolchain bundled with Visual Studio, and pins the MCP package version. Other clients can use their own supported launch command without this discovery logic.

When maintaining this example, change the pinned `@playwright/mcp` version in the launcher, run its `-ShowHelp` validation, and repeat the localhost navigation and screenshot smoke test before committing.

## Commands

### Standard validation

```powershell
.\tests\Scripts\RunGalleryPlaywrightTests.ps1 -Configuration Release
```

### Diagnostic commands

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
