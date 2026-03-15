# 07.08.01-contentservice-fix: Fix ContentService.cs for multi-targeting

## Objective
Apply conditional compilation to ContentService.cs to support both .NET Framework 4.7.2 and .NET 10.

## Scope
- Apply conditional compilation to NuGetGallery.Services/Storage/ContentService.cs:
  - #if NET472: use System.Web.IHtmlString
  - #else: use Microsoft.AspNetCore.Html.IHtmlContent
- Remove Storage\\*.cs exclusion for net10.0 in NuGetGallery.Services.csproj
- Add Microsoft.AspNetCore.Html package reference for net10.0

## Why This First
ContentService is referenced by controllers and views. Fixing it first unblocks the rest of the migration.

## Done When
- ContentService.cs compiles for both net472 and net10.0
- NuGetGallery.Services project builds without Storage\\*.cs exclusion
- Microsoft.AspNetCore.Html package added for net10.0
