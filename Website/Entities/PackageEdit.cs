using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    // This records the changes done in a package edit operation
    public class PackageEdit
    {
        public int Key { get; set; }

        [StringLength(128)]
        public string PackageId { get; set; }

        [StringLength(64)]
        public string Version { get; set; }

        [StringLength(64)]
        public string EditId { get; set; }

        [StringLength(32)]
        public string SecurityToken { get; set; } // Token used by the Worker to authenticate its callback to the Web Role

        public bool IsCompleted { get; set; }

        public Package Package { get; set; }
        public int PackageKey { get; set; }

        public string Authors { get; set; }

        public string Copyright { get; set; }

        public string Description { get; set; }

        public string Title { get; set; }
    }
}