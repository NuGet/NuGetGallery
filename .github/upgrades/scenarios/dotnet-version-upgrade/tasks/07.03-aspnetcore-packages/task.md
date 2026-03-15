# 07.03-aspnetcore-packages: Update to ASP.NET Core packages and net10.0

## Objective
Update target framework to net10.0 and replace ASP.NET Framework packages with ASP.NET Core equivalents.

## Scope
- Change TargetFramework from net472 to net10.0
- Replace ASP.NET MVC 5 → ASP.NET Core MVC packages
- Replace System.Web.* → Microsoft.AspNetCore.* packages
- Add ASP.NET Core framework references
- Remove incompatible packages (OWIN packages removed, replaced in 07.04)
- Keep Entity Framework 6 (EF6 supports .NET Core)

## Package Replacements
- Microsoft.AspNet.Mvc → (included in ASP.NET Core)
- System.Web → Microsoft.AspNetCore.Http.Abstractions
- System.Web.Optimization → WebOptimizer (or alternative from 07.01)
- Autofac.Mvc5 → Autofac.Extensions.DependencyInjection
- EntityFramework 6.x → (keep, add EF6 .NET Core support if needed)

## Done When
- TargetFramework is net10.0
- All ASP.NET Core packages added
- Project restores packages successfully (won't build yet - that's expected)
