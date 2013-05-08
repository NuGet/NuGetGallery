using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

namespace NuGetGallery.Statistics
{
    public class PackageDownloadDetailReport
    {
        public int Downloads { get; set; }

        [JsonProperty("Items")]
        public IEnumerable<PackageVersionDownloadDetailReport> Versions { get; set; }
    }

    public class PackageVersionDownloadDetailReport
    {
        public int Downloads { get; set; }
        public string Version { get; set; }

        [JsonProperty("Items")]
        public IEnumerable<PackageVersionClientDownloadDetailReport> Clients { get; set; }
    }

    public class PackageVersionClientDownloadDetailReport
    {
        public string Client { get; set; }
        public string ClientName { get; set; }
        public string ClientVersion { get; set; }
        public int Downloads { get; set; }
    }
}