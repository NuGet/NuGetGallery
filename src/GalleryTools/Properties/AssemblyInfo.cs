// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("GalleryTools")]
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

[assembly: AssemblyMetadata("RepositoryUrl", "https://www.github.com/NuGet/NuGetGallery")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
