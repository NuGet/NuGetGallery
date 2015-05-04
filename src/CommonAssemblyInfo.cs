using System;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyCompany(".NET Foundation")]
[assembly: AssemblyProduct("NuGet Services")]
[assembly: AssemblyCopyright("\x00a9 .NET Foundation. All rights reserved.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
[assembly: CLSCompliant(false)]
[assembly: NeutralResourcesLanguage("en-us")]

// The build will automatically inject the following attributes:
// AssemblyVersion, AssemblyFileVersion, AssemblyInformationalVersion, AssemblyMetadata (for Branch, CommitId, BuildDateUtc and RepositoryUrl)
