# 07.01-discovery: Discovery and Migration Strategy

## Objective
Analyze the NuGetGallery web application structure and define the migration strategy for ASP.NET Framework to ASP.NET Core.

## Scope
- Analyze current project structure (non-SDK-style, legacy .csproj)
- Document Web.config settings and how they map to appsettings.json + Program.cs
- Understand Admin area configuration and conditional compilation
- Identify all OWIN middleware and their ASP.NET Core equivalents
- Document bundling/minification approach (System.Web.Optimization → ASP.NET Core approach)
- Survey authentication providers (OWIN auth → ASP.NET Core Identity/Authentication)
- Catalog all incompatible packages and their replacements
- Define strategy for preserving two deployment artifacts (with/without Admin)

## Key Decisions Needed
1. **Admin Area Strategy**: Conditional compilation (#if ADMIN) vs runtime feature flags vs separate projects?
2. **Web.config Migration**: How to preserve environment-specific transforms (Web.Debug.config, Web.Release.config)?
3. **Bundling**: Use WebOptimizer, or modern approach (webpack/vite)?
4. **WCF Services**: Keep with CoreWCF, migrate to REST, or remove?

## Deliverables
- Document current architecture in task.md
- Migration strategy decision log
- List of all packages to replace
- Web.config → appsettings.json mapping
- Admin area build strategy (MSBuild conditions, publish profiles, etc.)

## Done When
- All decisions documented
- Migration approach approved (present to user if needed)
- Ready to proceed with conversion
