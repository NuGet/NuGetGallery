using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class UserCredential { 

        public string TenantId { get; set; }

        public string Type { get; set; }

        public string Value { get; set; }

    }
}