using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("NuGetGallery")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
[assembly: AssemblyCompany("Microsoft")]
[assembly: AssemblyProduct("NuGetGallery")]
[assembly: AssemblyCopyright("Copyright © Microsoft 2011")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: InternalsVisibleTo("NuGetGallery.Facts")]
[assembly: CLSCompliant(false)]
