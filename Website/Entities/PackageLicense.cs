using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace NuGetGallery.Entities
{
    public class PackageLicense
    {
        public int Key { get; set; }

        [Required]
        [StringLength(128)]
        public string Name { get; set; }

        public virtual ICollection<PackageLicenseReport> Reports { get; set; }
    }
}