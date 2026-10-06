# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: .NET 10 (`net10.0`)

## Scope
- Add a side-by-side ASP.NET Core project named `NuGetGallery.net10`.
- ASP.NET Core endpoints take precedence; unmatched requests use a catch-all YARP route to a configured legacy NuGetGallery origin.
- The new host must understand the legacy application's authentication cookie and ultimately be capable of issuing and refreshing the same cookie.
- A one-time sign-in disruption when the authentication format changes at an Azure App Service slot swap is acceptable.
- Keep the verified existing cookie name `.AspNet.LocalUser`; configure it explicitly in both hosts.
- Do not use top-level statements.
- Entity Framework 6 may remain and existing Gallery libraries should be reused even when they bring transitive dependencies.
- Entity Framework Core migration, controller migration, Azure deployment topology, and Aspire AppHost changes are out of scope.
- Azure resource access must follow existing Gallery storage, Key Vault, configuration, secret-injection, managed-identity, certificate, and connection-string patterns.

## Execution Constraints
- Complete the storage foundation and Key Vault foundation before migration-specific work.
- The storage foundation is a coordinated breaking source/API refactor. Update every affected consumer atomically, but preserve externally observable service behavior.
- Deliver the storage foundation as a self-contained commit that can be cherry-picked onto unmodified `dev`, built, deployed, and observed without .NET 10 migration behavior.
- Preserve existing filesystem implementation and behavior as much as possible. Move/extract existing code instead of rewriting it.
- During the storage foundation, add only the framework-neutral download-result refactor. Postpone file enumeration and all Data Protection capabilities until Data Protection integration.
- Deliver the Key Vault foundation as a separate self-contained commit that can be cherry-picked onto unmodified `dev`, built, deployed, and observed without behavior changes.
- Preserve `new KeyVaultReader(KeyVaultConfiguration)`, `ISecretReader`, and existing Key Vault secret/signing behavior.
- Migration integration must use the official `IKeyEncryptionKeyResolver` and Data Protection Key Vault extension rather than a custom XML encryption format.
- The legacy application remains the sole interactive sign-in authority and cookie refresher in this phase.
- Commit after each task so the two foundation changes remain independent.

## Source Control
- **Source Branch**: agr-scaling-carnival
- **Working Branch**: agr-scaling-carnival
- **Commit Strategy**: After Each Task
- **Branch Sync**: Disabled

## Build Tool Decisions
- **NuGetGallery.Core.csproj / NuGetGallery.Services.csproj**: `dotnet build` for targeted iteration (SDK-style; validate both `net472` and `netstandard2.1`).
- **NuGetGallery.net10.csproj / NuGetGallery.net10.Facts.csproj**: `dotnet build` / `dotnet test` (SDK-style, `net10.0`, no Visual Studio-only build features).
- **NuGetGallery.sln / NuGet.Server.Common.sln / NuGet.Jobs.sln / AccountDeleter.csproj**: Visual Studio MSBuild 18 (legacy web project and .NET Framework build surfaces). Use a short mapped repository drive for `NuGet.Jobs.sln` in this worktree to avoid Windows `MAX_PATH` failures in `Validation.PackageSigning.RevalidateCertificate.Tests`.
- **Long Paths**: Resolve worktree path-length failures through a shorter checkout/worktree root or mapped drive. Do not add per-project path-shortening overrides.

## User Preferences

### Custom Instructions

#### 10-auth-context-diagnostic
- User requested a test controller/action in the .NET 10 Gallery to validate that authentication context is available in both projects.
- This is a local authentication diagnostic, not a migration of existing legacy controllers. Preserve the other existing scope exclusions.
