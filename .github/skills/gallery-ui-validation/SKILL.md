---
name: gallery-ui-validation
description: "Use when implementing or changing user-visible Gallery behavior, adding C# Playwright coverage, or diagnosing Gallery browser-test failures. Guides the current implementing agent through the repository's browser-validation contract."
---

# Gallery UI validation

Read the [Playwright test contract](../../../tests/NuGetGallery.FunctionalTests/Playwright/README.md) before editing. It is authoritative for helpers, test structure, locators, commands, diagnosis, and reporting.

Keep this work in the agent already implementing the feature; do not dispatch a separate specialist agent.

1. Inspect the implementation, affected behavior, nearby browser tests, and shared helpers.
2. Implement the feature and focused committed C# coverage according to the contract.
3. Execute the contract's standard validation command.
4. Diagnose failures using the contract's guidance, correct the root cause, and rerun with the normal non-debug settings.
5. Report the change and validation evidence as specified by the contract, or state the execution blocker without claiming success.

Use available Playwright MCP tools only when useful. If unavailable, continue without them; the contract's optional browser-tooling section links client setup guidance. Do not automatically install tools or change client configuration.
