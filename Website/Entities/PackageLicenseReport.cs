using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Entities
{
    public class PackageLicenseReport
    {
        public int Key { get; set; }
        public int PackageKey { get; set; }
        public DateTime CreatedUtc { get; set; }
        public bool AllowLicenseReport { get; set; }
        public string ReportUrl { get; set; }
        public string Comment { get; set; }
        public virtual ICollection<PackageLicense> Licenses { get; set; }

        public virtual Package Package { get; set; }
    }
}
