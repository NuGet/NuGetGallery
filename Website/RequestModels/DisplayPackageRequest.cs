using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class DisplayPackageRequest
    {
        [Required]
        public string Id { get; set; }

        public string Version { get; set; }
    }
}