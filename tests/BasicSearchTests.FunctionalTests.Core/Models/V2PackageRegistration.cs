using System.Collections.Generic;

namespace BasicSearchTests.FunctionalTests.Core.Models
{
    public class V2PackageRegistration
    {
        public string Id { get; set; }

        public long DownloadCount { get; set; }

        public IList<string> Owners { get; set; }
    }
}
