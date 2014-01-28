using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Operations.Model
{
    public class TeamCityPackageSource : PackageSource
    {
        public static readonly string ElementName = "teamcity";

        public Version Version { get; set; }
        public string BuildType { get; set; }
        public Uri ServerUri { get; set; }
    }
}
