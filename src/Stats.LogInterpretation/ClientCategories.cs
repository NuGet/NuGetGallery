using System;
using System.Collections.Generic;
using System.Text;

namespace Stats.LogInterpretation
{
    /// <summary>
    /// Built from the SQL functions here: https://github.com/NuGet/NuGet.Jobs/tree/main/src/Stats.Warehouse/Programmability/Functions
    /// </summary>
    public static class ClientCategories
    {
        public const string NuGet = "NuGet";
        public const string WebMatrix = "WebMatrix";
        public const string NuGetPackageExplorer = "NuGet Package Explorer";
        public const string Script = "Script";
        public const string Crawler = "Crawler";
        public const string Mobile = "Mobile";
        public const string Browser = "Browser";
        public const string Unknown = "Unknown";
    }
}
