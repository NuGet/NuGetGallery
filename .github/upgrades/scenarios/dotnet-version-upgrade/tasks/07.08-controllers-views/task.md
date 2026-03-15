# 07.08-controllers-views: Update Controllers and Views for ASP.NET Core

## Objective
Update MVC controllers, views, and routing for ASP.NET Core compatibility.

## Scope
- Update controller base classes (System.Web.Mvc.Controller → Microsoft.AspNetCore.Mvc.Controller)
- Fix HttpContext usage (System.Web.HttpContext → Microsoft.AspNetCore.Http.HttpContext)
- Update routing to ASP.NET Core conventions
- Fix view compilation issues (Razor differences)
- Update Areas registration (Admin area routing)
- Apply ContentService.cs conditional compilation fix from Task 06 plan

## ContentService Migration
- Apply conditional compilation to NuGetGallery.Services/Storage/ContentService.cs:
  - #if NET472: use System.Web.IHtmlString
  - #else: use Microsoft.AspNetCore.Html.IHtmlContent
- Remove Storage\*.cs exclusion for net10.0 in NuGetGallery.Services
- Add Microsoft.AspNetCore.Html package reference for net10.0

## Admin Area
- Ensure Admin controllers only compile when admin feature enabled
- Verify Admin area routing works

## Dependencies
- Blocked on: 07.07 (need authentication working)

## Done When
- Controllers compile
- Views render
- Routing works
- Admin area compiles conditionally
- ContentService available for both net472 and net10.0
