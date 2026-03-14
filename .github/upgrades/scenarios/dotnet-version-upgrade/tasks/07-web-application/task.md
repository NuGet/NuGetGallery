# 07-web-application: Upgrade Main NuGetGallery Web Application

**This is the highest-risk task.** Upgrade src/NuGetGallery/NuGetGallery.csproj — the main ASP.NET Framework web application to ASP.NET Core on .NET 10.

**Critical scope**:
- 8,046+ LOC affected by API changes
- 8,880 issues related to ASP.NET Framework (System.Web) → ASP.NET Core
- Project uses non-SDK-style format — requires conversion to SDK-style
- 51 incompatible packages (Autofac.Mvc5, OWIN packages, ASP.NET MVC/WebAPI, Lucene.Net)
- Feature migrations required:
  - System.Web.Optimization bundling → ASP.NET Core bundling/minification
  - Classic Entity Framework initialization → EF6 on .NET Core setup (keep EF6 per user preference)
  - OWIN middleware → ASP.NET Core native middleware
  - WCF services → CoreWCF
- Heavy System.Web dependencies (HttpContext, MVC controllers, routing, authentication)

**Approach**:
- Convert project to SDK-style format first
- Update TFM to net10.0
- Migrate ASP.NET Framework packages to ASP.NET Core equivalents
- Convert OWIN middleware to ASP.NET Core middleware pipeline
- Replace bundling/minification with ASP.NET Core approach
- Update authentication/authorization to ASP.NET Core Identity
- Keep Entity Framework 6 (EF6 supports .NET Core/10)
- Address WCF service hosting (CoreWCF or alternative)

**Done when**:
- Project converted to SDK-style
- Targets net10.0
- ASP.NET Core packages configured
- Middleware pipeline converted from OWIN to ASP.NET Core
- Bundling/minification replaced
- Authentication working
- Entity Framework 6 configured for .NET Core
- WCF services addressed
- Project builds without errors
- Application runs and serves requests
- Critical functionality validated (user auth, package search, package pages)

