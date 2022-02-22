using System;
using System.Collections.Generic;
using System.Text;

namespace Stats.LogInterpretation
{
    /// <summary>
    /// Below exhaustive list of client names has been built from existing SQL functions.
    /// These can be found here: https://github.com/NuGet/NuGet.Jobs/tree/main/src/Stats.Warehouse/Programmability/Functions
    /// </summary>
    public static class ClientNames
    {
        public static IReadOnlyList<string> NuGet = new List<string>()
        {
            "NuGet Cross-Platform Command Line",
            "NuGet Client V3",

            //-- VS NuGet 4.6+
            "NuGet VS VSIX",

            //-- VS NuGet 2.8+
            "NuGet VS PowerShell Console",
            "NuGet VS Packages Dialog - Solution",
            "NuGet VS Packages Dialog",
            "NuGet Shim",

            //-- VS NuGet (pre-2.8)
            "NuGet Add Package Dialog",
            "NuGet Command Line",
            "NuGet Package Manager Console",
            "NuGet Visual Studio Extension",
            "Package-Installer",

            //-- dotnet restore / msbuild /t:Restore
            "NuGet MSBuild Task",
            "NuGet .NET Core MSBuild Task",
            "NuGet Desktop MSBuild Task",
        };

        public static IReadOnlyList<string> WebMatrix = new List<string>()
        {
            "WebMatrix"
        };

        public static IReadOnlyList<string> NuGetPackageExplorer = new List<string>()
        {
            "NuGet Package Explorer Metro",
            "NuGet Package Explorer"
        };

        public static IReadOnlyList<string> Script = new List<string>()
        {
            "Powershell",
            "curl",
            "Wget",
            "Java"
        };

        public static IReadOnlyList<string> Crawler = new List<string>()
        {
            "Bot",
            "bot",
            "Slurp",
            "BingPreview",
            "crawler",
            "sniffer",
            "spider"
        };

        public static IReadOnlyList<string> Mobile = new List<string>()
        {
            "Mobile",
            "Android",
            "Kindle",
            "BlackBerry",
            "Openwave",
            "NetFront",
            "CFNetwork",
            "iLunascape"
        };

        public static IReadOnlyList<string> Browser = new List<string>()
        {
            "Mozilla",
            "Firefox",
            "Opera",
            "Chrome",
            "Chromium",
            "Internet Explorer",
            "Browser",
            "Safari",
            "Sogou Explorer",
            "Maxthon",
            "SeaMonkey",
            "Iceweasel",
            "Sleipnir",
            "Konqueror",
            "Lynx",
            "Galeon",
            "Epiphany",
            "Lunascape"
        };

        public static IReadOnlyList<string> AbsoluteBrowserNames = new List<string>()
        {
            "IE",
            "Iron"
        };

        public static IReadOnlyList<string> Unknown = new List<string>()
        {
            "PhantomJS",
            "WebKit Nightly",
            "Python Requests",
            "Jasmine",
            "Java",
            "AppleMail",
            "NuGet Test Client"
        };
    }
}
