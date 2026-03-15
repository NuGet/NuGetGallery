# 07.01-discovery: Discovery and Migration Strategy

## Objective
Analyze the NuGetGallery web application structure and define the migration strategy for ASP.NET Framework to ASP.NET Core.

## Current Architecture Analysis

### Project Structure
- **Type**: Legacy non-SDK-style .csproj (Web Application Project)
- **Framework**: .NET Framework 4.7.2
- **Project GUID**: {1DACF781-5CD0-4123-8BAC-CD385D864BE5}
- **Project Type**: ASP.NET MVC 5 Web Application
- **Files**: 1,329 total files

### Key Directories
- **App_Start**: Configuration classes (OwinStartup, AutofacConfig, WebApiConfig, Routes, etc.)
- **Areas/Admin**: Admin-specific controllers, views, models, services (Dynamic Data, EF migrations)
- **Authentication**: Custom auth providers and OWIN integration
- **Controllers**: Main MVC controllers
- **Views**: Razor views
- **Scripts**: Client-side JavaScript
- **Content**: CSS and static assets
- **OData**: OData feed configuration
- **WebApi**: Web API 2 controllers

### Admin Area Configuration
- **Current approach**: Runtime feature flag (`Gallery.AdminPanelEnabled` in Web.config)
- **No conditional compilation**: DefineConstants shows only DEBUG/TRACE, no ADMIN symbol
- **Admin controllers**: Located in Areas/Admin/Controllers
- **Admin views**: Located in Areas/Admin/Views
- **Dynamic Data**: Uses ASP.NET Dynamic Data for database admin (PageTemplates, FieldTemplates)
- **Separate DbContext**: SupportRequestContext with EF migrations in Areas/Admin/Migrations

### OWIN Middleware Stack (from OwinStartup.cs)
1. **Autofac IoC injection** (`UseAutofacInjection`)
2. **Force SSL middleware** (`UseForceSsl`) - custom NuGet.Services.Owin middleware
3. **Cookie Authentication** (`UseCookieAuthentication`) - for external auth
4. **Custom authenticators** - LocalUserAuthenticator and other providers via AuthenticationService

### Authentication Providers
- **LocalUser**: Cookie-based authentication (primary)
- **Microsoft Account**: OAuth (configurable via Auth.MicrosoftAccount.Enabled)
- **Azure AD v2**: OAuth (configurable via Auth.AzureActiveDirectoryV2.Enabled)
- **API Key**: Token-based auth for NuGet client push/pull
- All configured through OWIN middleware in OwinStartup.Configuration

### Bundling and Minification
- **Assessment shows**: 23 occurrences of System.Web.Optimization usage
- **No BundleConfig.cs found**: Search returned no results
- **Likely inline bundling** or removed in previous refactor
- **Strategy**: Direct script/link tags to wwwroot/ (ASP.NET Core approach)

### WCF Services
- **Assessment detected**: 3 optional WCF service migration warnings
- **Used for**: Likely OData feeds (Microsoft.Data.Services 5.8.4 package)
- **WCF Data Services**: Uses classic OData v1-v4 endpoints

### Incompatible Packages (from assessment)
**Mandatory replacements (29 packages incompatible with .NET 10):**
- System.Web.* (bundled in framework reference)
- Microsoft.AspNet.Mvc, WebApi, Razor, WebPages (→ ASP.NET Core MVC)
- Microsoft.Owin.* packages (→ ASP.NET Core middleware)
- Microsoft.AspNet.Web.Optimization (→ remove, use direct tags)
- Microsoft.AspNet.DynamicData (→ no direct replacement, needs custom solution)

**Recommended upgrades (8 packages):**
- Various Microsoft.AspNet packages to latest compatible versions

**Compatible packages (remain as-is):**
- EntityFramework 6.5.1 ✅ (user preference: keep EF6, do NOT migrate to EF Core)
- Autofac 4.9.1 and related packages ✅
- Application Insights packages ✅
- Various utility packages (CommonMark, HtmlSanitizer, Lucene.Net, etc.) ✅

### Key Configuration Settings (Web.config → appsettings.json)
**Storage:**
- Gallery.StorageType (FileSystem/AzureStorage)
- Gallery.AzureStorage.*.ConnectionString (multiple connection strings)

**Feature flags:**
- Gallery.AdminPanelEnabled
- Gallery.AdminPanelDatabaseAccessEnabled
- Gallery.AsynchronousPackageValidationEnabled
- Gallery.SelfServiceAccountDeleteEnabled

**Auth settings:**
- Auth.LocalUser.Enabled
- Auth.MicrosoftAccount.* (ClientId, ClientSecret)
- Auth.AzureActiveDirectoryV2.*

**Service Bus:**
- AzureServiceBus.*.ConnectionString (multiple topics)

**Application settings:**
- Gallery.Environment
- Gallery.SiteRoot
- Gallery.AppInsightsInstrumentationKey
- Many other settings...

### Breaking Changes Summary (from assessment)
- **8,129 total issues** to address
- **API binary incompatible**: 6,992 issues (System.Web APIs)
- **API source incompatible**: 837 issues
- **Behavioral changes**: 217 issues
- **System.Web APIs**: 7,670 issues (largest category)

## Migration Strategy Decisions

### 1. Admin Area Strategy
**Decision**: **Runtime feature flags** (keep current approach)

**Rationale**:
- Current implementation already uses runtime flags (`Gallery.AdminPanelEnabled`)
- No conditional compilation currently used
- ASP.NET Core supports this pattern easily via middleware/filters
- Avoids project split complexity
- Preserves existing architecture pattern

**Implementation**:
- Keep Areas/Admin structure
- Use ASP.NET Core Area routing
- AdminActionAttribute filter checks feature flag
- Single deployment artifact with runtime toggle

### 2. Web.config → Configuration Migration
**Decision**: **Migrate to appsettings.json + environment-specific files**

**Approach**:
- `appsettings.json` - default/local dev settings
- `appsettings.Development.json` - dev overrides
- `appsettings.Production.json` - production overrides
- Azure App Service configuration overrides for sensitive settings
- Preserve all existing configuration keys in GalleryConfigurationService

### 3. Bundling and Minification
**Decision**: **Remove bundling, use direct tags** (per skill guidance)

**Rationale**:
- Assessment shows only 23 System.Web.Optimization usages
- No BundleConfig.cs found (already minimal)
- Modern approach: direct script/link tags + CDN for libraries
- ASP.NET Core static file middleware serves from wwwroot/
- Minified versions already exist in Scripts folder

**Implementation**:
- Replace any remaining @Scripts.Render/@Styles.Render with direct tags
- Move Content/ and Scripts/ to wwwroot/
- Update paths in views

### 4. WCF/OData Services
**Decision**: **Keep OData with ASP.NET Core OData libraries**

**Rationale**:
- NuGet Gallery's core API is OData v1/v2 feeds
- ASP.NET Core has OData support (Microsoft.AspNetCore.OData)
- Assessment marks as "optional" - not blocking
- Migration to REST would be massive breaking change for clients

**Implementation**:
- Migrate OData configuration to ASP.NET Core OData 8.x
- Update feed controllers to use ASP.NET Core patterns
- Preserve feed compatibility (critical for NuGet clients)

### 5. OWIN → ASP.NET Core Middleware
**Decision**: **Migrate to native ASP.NET Core middleware**

**Key migrations**:
- OWIN cookie auth → ASP.NET Core Cookie Authentication
- Custom ForceSsl middleware → HTTPS redirection middleware (or keep custom)
- IAppBuilder → IApplicationBuilder in Program.cs
- Startup.Configuration → Program.cs ConfigureServices + Configure

### 6. Dynamic Data (Admin area)
**Decision**: **Defer to later subtask - assess alternatives**

**Options**:
1. Custom admin UI (Razor Pages or MVC views)
2. Third-party admin framework (e.g., AdminLTE, ASP.NET Core Scaffold-DbContext with custom views)
3. Remove and replace with simpler CRUD

**Note**: This is a significant effort - will need dedicated subtask

## Package Migration Plan

**Remove (incompatible):**
- Microsoft.AspNet.Mvc
- Microsoft.AspNet.WebApi.*
- Microsoft.AspNet.Razor/WebPages
- Microsoft.Owin.* packages
- Microsoft.AspNet.Web.Optimization
- System.Web.* (implicit in net472, not needed in net10)

**Add (ASP.NET Core replacements):**
- Microsoft.AspNetCore.App (metapackage - includes MVC, auth, etc.)
- Microsoft.AspNetCore.OData (for OData feeds)
- Possibly CoreWCF (if WCF services beyond OData)

**Keep (compatible):**
- EntityFramework 6.5.1
- Autofac family
- Application Insights
- Utility libraries

## Risks and Open Questions

1. **Dynamic Data complexity**: No direct ASP.NET Core equivalent - needs custom solution
2. **OData compatibility**: NuGet clients depend on v1/v2 feeds - must preserve exact behavior
3. **Test coverage**: 8,000+ API issues means extensive testing required
4. **Admin area testing**: Less commonly used, may have hidden dependencies
5. **Machine key configuration**: Custom GalleryMachineKeyConfigurationProvider needs investigation

## Next Steps (Execution Order)
1. **07.02**: Convert to SDK-style project (structural conversion)
2. **07.03**: Update packages to ASP.NET Core + net10.0 target
3. **07.04**: Migrate Web.config → appsettings.json
4. **07.05**: Create Program.cs, migrate Startup logic
5. **07.06**: Migrate OWIN middleware to ASP.NET Core
6. **07.07**: Migrate authentication providers
7. **07.08**: Update controllers and views
8. **07.09**: Migrate bundling (remove optimization, direct tags)
9. **07.10**: Create publish profiles (no conditional compilation needed)
10. **07.11**: Validation and testing
11. **07.12**: Discuss Admin area physical file cleanup

## Decision Log
- **2026-03-15**: Admin area - keep runtime feature flags (no conditional compilation)
- **2026-03-15**: Configuration - migrate to appsettings.json with environment files
- **2026-03-15**: Bundling - remove System.Web.Optimization, use direct tags
- **2026-03-15**: OData - migrate to ASP.NET Core OData (preserve feed compatibility)
- **2026-03-15**: Dynamic Data - defer detailed plan to future subtask
