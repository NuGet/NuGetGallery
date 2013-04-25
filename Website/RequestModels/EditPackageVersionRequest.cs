using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class EditPackageVersionRequest
    {
        [StringLength(256)]
        public string Title { get; set; }

        [StringLength(512)]
        public string Authors { get; set; }

        [StringLength(512)]
        public string Copyright { get; set; }

        public string ReleaseNotes { get; set; }
    }
}