# 07.08.03-views-migration: Migrate Razor views to ASP.NET Core

## Objective
Fix Razor views to compile under ASP.NET Core.

## Scope
- Create/update _ViewImports.cshtml with ASP.NET Core namespaces:
  - @using Microsoft.AspNetCore.Mvc
  - @using Microsoft.AspNetCore.Mvc.Rendering
  - @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
- Migrate @helper directives (not supported in ASP.NET Core):
  - Convert to partial views or HTML helpers
  - Update App_Code/ViewHelpers.cshtml
- Fix System.Web.Mvc references in views:
  - System.Web.Mvc.Html → Microsoft.AspNetCore.Mvc.Rendering
  - System.Web.Routing → Microsoft.AspNetCore.Routing
  - MvcHtmlString → IHtmlContent
- Update view base types if needed

## Known Issues
- @helper directive not supported → need alternative approach
- ViewBag/ViewData should work as-is
- @Html helpers mostly compatible but different namespace

## Done When
- _ViewImports.cshtml created/updated
- All @helper directives migrated
- Views compile without System.Web.Mvc references
- No RZ1002 or RZ1005 Razor compilation errors
