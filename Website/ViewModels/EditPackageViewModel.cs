using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class EditPackageViewModel
    {
        public string Title { get; set; }
        public string Version { get; set; }
        public EditPackageRequest EditPackageRequest { get; set; }
    }
}