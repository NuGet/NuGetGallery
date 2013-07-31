using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class PlanMergeViewModel
    {
        public User SourceUser { get; set; }
        public User TargetUser { get; set; }
        public IEnumerable<Package> TargetPackages { get; set; }
        public IEnumerable<Package> SourcePackages { get; set; }
    }
}