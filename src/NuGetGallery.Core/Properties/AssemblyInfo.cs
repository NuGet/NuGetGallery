// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly: AssemblyCopyright("\x00a9 .NET Foundation. All rights reserved.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

#if !PORTABLE
[assembly: ComVisible(false)]
#endif

[assembly: CLSCompliant(false)]
[assembly: NeutralResourcesLanguage("en-us")]

[assembly: AssemblyDescription("Core support library for NuGet Gallery Frontend and Backend")]

// The build will automatically inject the following attributes:
// AssemblyVersion, AssemblyFileVersion, AssemblyInformationalVersion, AssemblyMetadata (for Branch, CommitId, and BuildDateUtc)

[assembly: AssemblyMetadata("RepositoryUrl", "https://www.github.com/NuGet/NuGetGallery")]