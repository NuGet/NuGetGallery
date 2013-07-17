using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace NuGetGallery.Entities
{
    public class PackageLicenseReport
    {
        public int Key { get; set; }

        [Required]
        public int PackageKey { get; set; }
        
        [Required]
        public DateTime CreatedUtc { get; set; }

        [Required]
        public int Sequence { get; set; }

        [Required]
        [StringLength(256)]
        public string ReportUrl { get; set; }

        [StringLength(256)]
        public string Comment { get; set; }

        public virtual ICollection<PackageLicense> Licenses { get; set; }

        public virtual Package Package { get; set; }
    }
}
