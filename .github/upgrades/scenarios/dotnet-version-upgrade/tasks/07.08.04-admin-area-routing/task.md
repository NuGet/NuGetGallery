# 07.08.04-admin-area-routing: Configure Admin area routing

## Objective
Ensure Admin area routing works in ASP.NET Core.

## Scope
- Update Admin area registration in Program.cs:
  - Configure area routes using MapAreaControllerRoute()
  - Set up conventional routing for Admin controllers
- Verify Admin controllers have [Area("Admin")] attribute
- Test Admin area URL patterns work

## Note
Admin controllers conditional compilation is handled in task 07.12-admin-physical-removal.
This task only ensures routing works when Admin controllers are present.

## Done When
- Admin area routes configured in Program.cs
- Admin controllers accessible at /Admin/* URLs
- Area routing follows ASP.NET Core conventions
