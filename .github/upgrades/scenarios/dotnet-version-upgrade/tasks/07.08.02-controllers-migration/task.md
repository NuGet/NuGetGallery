# 07.08.02-controllers-migration: Migrate controllers to ASP.NET Core MVC

## Objective
Update MVC controllers to use ASP.NET Core APIs instead of System.Web.Mvc.

## Scope
- Update controller base classes:
  - System.Web.Mvc.Controller → Microsoft.AspNetCore.Mvc.Controller
  - System.Web.Mvc.ActionResult → Microsoft.AspNetCore.Mvc.IActionResult
- Fix HttpContext usage:
  - HttpContext.GetOwinContext() → HttpContext (native)
  - System.Web.HttpContext → Microsoft.AspNetCore.Http.HttpContext
- Update authentication usage:
  - OwinContext.Authentication.SignIn() → HttpContext.SignInAsync()
  - OwinContext.Authentication.SignOut() → HttpContext.SignOutAsync()
- Update routing attributes (if needed)
- Fix action result types (JsonResult, RedirectResult, etc.)

## Key Migrations
- Request.IsAuthenticated → User.Identity.IsAuthenticated
- Request.Url → Request.GetDisplayUrl()
- Response.StatusCode = 404 → return NotFound()
- Server.MapPath() → IWebHostEnvironment.ContentRootPath

## Done When
- All controllers compile without System.Web.Mvc references
- HttpContext usage migrated to ASP.NET Core
- Authentication calls use ASP.NET Core APIs
- Controller actions return ASP.NET Core action results
