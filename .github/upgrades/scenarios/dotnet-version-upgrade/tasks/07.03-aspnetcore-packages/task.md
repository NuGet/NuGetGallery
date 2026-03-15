# 07.03-aspnetcore-packages: Migrate to SDK-style + ASP.NET Core + net10.0

## Objective
Perform complete migration from ASP.NET Framework to ASP.NET Core in one coordinated step: convert project format, update target framework, and replace packages.

## Important Context
ASP.NET Framework web applications **cannot** be incrementally converted to SDK-style while staying on net472. The migration must happen all at once:
- Legacy .csproj format (net472) → SDK-style format (net10.0)
- ASP.NET Framework → ASP.NET Core
- System.Web APIs → Microsoft.AspNetCore APIs

This is the standard migration path for ASP.NET Framework web applications.

## Scope
**1. Convert to SDK-style project:**
- Manually rewrite .csproj to `<Project Sdk="Microsoft.NET.Sdk.Web">`
- Remove verbose file listings (SDK includes by glob)
- Preserve necessary custom configurations
- Convert packages.config → PackageReference

**2. Update target framework:**
- Change TargetFramework from net472 to net10.0

**3. Replace packages:**
- Microsoft.AspNet.* → ASP.NET Core packages (included in framework)
- System.Web.* → Microsoft.AspNetCore.* equivalents
- Autofac.Mvc5 → Autofac.Extensions.DependencyInjection
- Remove OWIN packages (migration handled in 07.06)
- Keep Entity Framework 6.5.1 (EF6 supports .NET Core, per user preference)

**4. Move static files:**
- Move Content/ and Scripts/ to wwwroot/

## Package Replacements
- Microsoft.AspNet.Mvc (removed - included in framework)
- System.Web → Microsoft.AspNetCore.Http
- System.Web.Optimization → (remove, handled in 07.09)
- Autofac.Mvc5, Autofac.Mvc5.Owin → Autofac.Extensions.DependencyInjection
- Microsoft.Owin.* → (remove, replaced in 07.06)
- EntityFramework 6.5.1 → (keep, add Microsoft.EntityFrameworkCore.Design for tooling)

## Risks
- Large number of compile errors expected (8000+ API issues from assessment)
- Dynamic Data admin area will not compile (System.Web.DynamicData doesn't exist in .NET 10)
- ASPX/ASCX files will need replacement or removal
- This task gets the structure right; subsequent tasks will fix code

## Done When
- Project is SDK-style format
- TargetFramework is net10.0
- ASP.NET Core packages added
- Project restores packages successfully
- Static files moved to wwwroot/
- Build will fail (expected - code fixes in subsequent tasks)
