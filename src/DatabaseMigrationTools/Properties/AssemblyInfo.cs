// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("DatabaseMigrationTools")]
[assembly: AssemblyCompany(".NET Foundation")]
[assembly: AssemblyProduct("NuGet Services")]
[assembly: AssemblyCopyright("\x00a9 .NET Foundation. All rights reserved.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

#if !PORTABLE
[assembly: ComVisible(false)]
#endif

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: CLSCompliant(false)]
[assembly: NeutralResourcesLanguage("en-us")]

[assembly: AssemblyDescription("Database migration tools for NuGet Gallery/Support Requests/Validation databases")]

// The build will automatically inject the following attributes:
// AssemblyVersion, AssemblyFileVersion, AssemblyInformationalVersion, AssemblyMetadata (for Branch, CommitId, and BuildDateUtc)

[assembly: AssemblyMetadata("RepositoryUrl", "https://www.github.com/NuGet/NuGetGallery")]
