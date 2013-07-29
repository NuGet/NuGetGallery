using System.Collections.Generic;

namespace NuGetGallery
{
    public class CuratedFeedViewModel
    {
        public string Name { get; set; }
        public IEnumerable<string> Managers { get; set; }
        public IEnumerable<string> ExcludedPackages { get; set; }
        public IEnumerable<IncludedPackage> IncludedPackages { get; set; }

        public class IncludedPackage
        {
            public bool AutomaticallyCurated { get; set; }
            public string Id { get; set; }
        }
    }
}