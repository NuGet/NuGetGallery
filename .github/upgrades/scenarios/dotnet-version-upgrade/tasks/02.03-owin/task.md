# 02.03-owin: Upgrade NuGet.Services.Owin

# 02.03: Upgrade NuGet.Services.Owin

## Objective
Upgrade OWIN compatibility library. Keep Microsoft.Owin package - it's marked incompatible but may be needed for compatibility shim.

## Projects
- src/NuGet.Services.Owin/NuGet.Services.Owin.csproj (net472)

## Changes
- Update TargetFramework from net472 to net472;net10.0
- Keep Microsoft.Owin 4.2.2 (compatibility adapter)
- Address any API compatibility issues if build fails

## Done When
- Project multi-targets net472;net10.0
- Project builds (may have warnings about incompatible packages)
- Higher tiers still build
