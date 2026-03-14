# 06-service-layer: Upgrade Tier 6 Service Layer

Upgrade 3 projects in Tier 6: NuGet.Jobs.Common (shared job infrastructure), NuGetGallery.Services (web services layer), NuGetGallery.Core.Facts (test library).

These projects depend on NuGetGallery.Core and provide job infrastructure and web service implementations.

**Key concerns**:
- NuGetGallery.Services: 472+ API issues, 12 incompatible packages (OWIN, ASP.NET MVC)
- Heavy System.Web dependencies in NuGetGallery.Services
- NuGet.Jobs.Common: consolidates multiple service dependencies

**Done when**:
- All 3 projects target net10.0
- OWIN/ASP.NET packages addressed (compatibility adapters or alternatives)
- Projects build without errors
- Tests pass
- Main web app (Tier 7) still builds on old framework

