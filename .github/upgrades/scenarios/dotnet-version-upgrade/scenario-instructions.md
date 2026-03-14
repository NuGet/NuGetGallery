# .NET 10.0 Upgrade - NuGet Gallery

## Strategy
**Bottom-Up (Dependency-First)** — Upgrade from leaf nodes to root applications, tier by tier.

**Rationale**: 39 projects with 9-level dependency graph. Main web application (NuGetGallery.csproj) has high complexity (8000+ API issues, ASP.NET Framework → ASP.NET Core migration, OWIN → native middleware). Bottom-up approach validates foundation libraries first, then progressively moves through dependency chain to complex web app, minimizing risk through tier-by-tier validation.

## Preferences
- **Flow Mode**: Automatic
- **Commit Strategy**: After Each Task
- **Pace**: Standard

## Execution Constraints
- Strict tier ordering: Tier N must complete and validate before Tier N+1
- Between-tier validation: after each tier completes, confirm higher tiers (still on old framework) still build
- Per-tier flow: update all projects in tier → restore/build → fix errors → run tests → validate higher tiers → mark complete
- Tests for Tier N projects run immediately after Tier N upgrade (not deferred to end)

## User Preferences

### Technical Preferences
- **Entity Framework**: Keep EF6 — do NOT migrate to EF Core (user preference, 2025-01-21)
- Follow existing code style (tabs, braces on new lines, spaces before parentheses)
- Maintain compatibility with nullable reference types where enabled
- Preserve existing async/await patterns

## Key Decisions Log
(Will be populated as decisions are made during execution)
