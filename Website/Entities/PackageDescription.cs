using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    // Note: Not actually an entity!
    public class PackageDescription
    {
        public string Authors { get; set; }
        public string Copyright { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }
        public string ProjectUrl { get; set; }

        public string IconUrl { get; set; }

        public string HashAlgorithm { get; set; }

        public string Hash { get; set; }

        public long PackageFileSize { get; set; }

        public string ReleaseNotes { get; set; }

        public string Tags { get; set; }

        public string Summary { get; set; }
    }
}