---
name: gallery-ui-validation
description: Add or update Gallery Playwright coverage and complete the Aspire-hosted validation loop.
agent: Gallery UI validation
argument-hint: Describe the user-visible feature or provide the current diff to validate.
---

Analyze the requested feature and current diff for user-visible Gallery behavior.

Follow `tests/NuGetGallery.FunctionalTests/Playwright/README.md` and:

1. Find the closest existing C# Playwright test and reusable helpers.
2. Implement or correct the feature.
3. Add or update maintainable committed Playwright coverage with stable locators and web-first assertions.
4. Use Playwright MCP against localhost only when exploratory inspection or debugging helps; do not treat MCP activity as a substitute for the committed test.
5. Run `.\tests\Scripts\RunGalleryPlaywrightTests.ps1 -Configuration Release`.
6. Diagnose and repair failures, then rerun until the normal non-debug suite passes.
7. Return PR-ready evidence containing the test changed, exact command, and passed/failed/skipped counts. State any harness blocker instead of claiming success.
