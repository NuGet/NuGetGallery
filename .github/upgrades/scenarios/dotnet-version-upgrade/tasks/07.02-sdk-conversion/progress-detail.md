# Task 07.02-sdk-conversion Progress Detail

## What Was Attempted
- Ran SDK-style conversion tool on NuGetGallery.csproj
- Attempted to fix build errors by adding missing System.Web.* references
- Encountered 14 build errors related to Dynamic Data components

## Issue Discovered
**ASP.NET Framework web applications cannot be reliably converted to SDK-style while still targeting .NET Framework 4.7.2.**

The SDK-style project format (`<Project Sdk="Microsoft.NET.Sdk.Web">`) is designed for:
- .NET Core / .NET 5+ applications  
- ASP.NET Core (not ASP.NET Framework)

**Why this failed:**
- ASP.NET Framework apps with Web Forms, Dynamic Data, ASPX/ASCX files rely on legacy .csproj format
- System.Web.* APIs and ASP.NET Framework tooling require traditional project structure
- SDK-style conversion attempted to modernize the project format without changing the framework

## Correct Approach
**ASP.NET Framework → ASP.NET Core migration must happen in one step:**
- Stay in legacy .csproj format while on .NET Framework 4.7.2
- Convert to SDK-style + ASP.NET Core + net10.0 **simultaneously** (not incrementally)

This is the standard migration path for ASP.NET Framework web applications.

## Actions Taken
- Reverted NuGetGallery.csproj to original legacy format
- Updated task plan to skip standalone SDK-style conversion
- Task 07.03 will handle the complete migration (SDK-style + ASP.NET Core + net10.0)

## Build/Test Results
N/A - reverted before completion

## Next Steps
Skip to Task 07.03: Combined SDK-style conversion + ASP.NET Core migration + net10.0 upgrade
