# 07.09-bundling-minification: Migrate bundling and minification

## Objective
Replace System.Web.Optimization bundling with ASP.NET Core approach (determined in 07.01).

## Scope
- Remove System.Web.Optimization references
- Implement chosen bundling solution (WebOptimizer, webpack, or other)
- Migrate bundle configurations from App_Start/BundleConfig.cs
- Update views to reference new bundle paths
- Configure minification for production

## Dependencies
- Blocked on: 07.08 (need views working)

## Done When
- Bundling works
- Minification works in production
- CSS/JS load correctly
- Performance acceptable
