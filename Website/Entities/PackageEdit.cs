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

        public string Hash { get; set; }

        public string HashAlgorithm { get; set; }

        public string IconUrl { get; set; }

        public long PackageFileSize { get; set; }

        public string ProjectUrl { get; set; }

        public string ReleaseNotes { get; set; }

        public string Summary { get; set; }

        public string Tags { get; set; }

        public string Title { get; set; }
    }
}