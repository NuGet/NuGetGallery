using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("GitHubVulnerabilities2Db")]
[assembly: AssemblyDescription("Background job to ingest GitHub vulnerability data into Gallery database.")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
[assembly: AssemblyCompany(".NET Foundation")]
[assembly: AssemblyProduct("GitHubVulnerabilities2Db")]
[assembly: AssemblyCopyright("\x00a9 .NET Foundation. All rights reserved.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("26bb718a-e1c1-4e70-9008-fb8ee7a7f7e5")]

// The build will automatically inject the following attributes:
// AssemblyVersion, AssemblyFileVersion, AssemblyInformationalVersion, AssemblyMetadata (for Branch, CommitId, and BuildDateUtc)
[assembly: AssemblyMetadata("RepositoryUrl", "https://www.github.com/NuGet/NuGetGallery")]