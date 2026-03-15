# 07.07-authentication Progress Detail

## Completed: ASP.NET Core Authentication Configuration

### Changes Made

**Program.cs modifications:**

1. **Added authentication configuration** (after session configuration, before Application Insights)
   - Configured multi-scheme authentication with LocalUser as default and External for sign-in
   - Added LocalUser cookie authentication scheme:
     - Login path: `/users/account/LogOn`
     - 6-hour expiration with sliding window
     - Cookie security policy based on Gallery:RequireSSL configuration
   - Added External cookie authentication scheme (for external auth flow):
     - 5-minute expiration
     - Used as intermediate cookie during external authentication

2. **External authentication providers:**
   - **Microsoft Account**: Configured with client ID/secret from configuration
     - Scopes: `wl.emails`, `wl.signin`
     - Sign-in scheme: External
   - **Azure Active Directory v2** (OpenID Connect): Configured with client ID/secret
     - Authority: `https://login.microsoftonline.com/{tenantId}/v2.0` (defaults to "common")
     - Callback path: `/users/account/authenticate/return`
     - Scopes: openid, profile, email
     - Gets claims from UserInfo endpoint

3. **Enabled authentication/authorization middleware:**
   - Uncommented `app.UseAuthentication()` and `app.UseAuthorization()`
   - Correct position in pipeline: after UseRouting, before UseSession

4. **Added necessary using statements:**
   - `Microsoft.AspNetCore.Authentication.Cookies`
   - `Microsoft.AspNetCore.Builder`
   - `Microsoft.AspNetCore.Hosting`
   - `Microsoft.Extensions.Configuration`
   - `Microsoft.Extensions.DependencyInjection`
   - `Microsoft.Extensions.Hosting`
   - `System.Collections.Generic`
   - `System.Threading.Tasks`

### Configuration Required

Authentication providers require appsettings.json entries:

```json
{
  "Auth": {
    "MicrosoftAccount": {
      "ClientId": "your-client-id",
      "ClientSecret": "your-client-secret"
    },
    "AzureActiveDirectoryV2": {
      "ClientId": "your-client-id",
      "ClientSecret": "your-client-secret",
      "TenantId": "common"
    }
  }
}
```

### Build Status

**Expected failures** - build currently fails with ~500 errors:
- Views still reference System.Web.Mvc (will be fixed in task 07.08)
- @helper directive not supported in ASP.NET Core Razor (will be migrated in task 07.08)
- Controllers still use OWIN authentication (will be updated in task 07.08)
- Some projects still target .NET Framework 4.7.2 (will be upgraded in task 08)

These errors are expected at this stage. The authentication configuration in Program.cs is complete and correct.

### Files Modified

- `src/NuGetGallery/Program.cs` - Added authentication configuration

### Next Steps (Task 07.08)

- Migrate controllers from OWIN authentication to ASP.NET Core (`HttpContext.GetOwinContext().Authentication` → `HttpContext`)
- Update views and Razor files to use ASP.NET Core namespaces
- Migrate @helper directives to tag helpers or HTML helpers
- Update view imports
