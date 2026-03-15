# 07.02-sdk-conversion: Convert to SDK-style project

## Objective
Convert NuGetGallery.csproj from legacy non-SDK-style to modern SDK-style format.

## Scope
- Convert project file to SDK-style (<Project Sdk="Microsoft.NET.Sdk.Web">)
- Preserve all existing files (controllers, views, Areas folder structure)
- Migrate from packages.config to PackageReference (use Central Package Management)
- Keep target framework as net472 initially (don't break yet)
- Preserve Admin area conditional compilation setup from discovery

## Approach
1. Load converting-to-sdk-style skill for guidance
2. Create backup of current .csproj
3. Convert to SDK-style incrementally:
   - Update project SDK attribute
   - Remove verbose ItemGroup entries (SDK includes by default)
   - Explicitly include/exclude Admin area files based on strategy from 07.01
   - Migrate PackageReferences from packages.config
   - Preserve custom MSBuild properties and targets
4. Validate builds on net472 (no breaking changes yet)

## Dependencies
- Blocked on: 07.01 (need Admin area strategy)

## Done When
- Project file is SDK-style
- Still targets net472
- Solution builds successfully
- Admin area conditional compilation works
- Two artifacts can still be produced
