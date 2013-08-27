using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class ConfirmationRequiredViewModel
    {
        public string UserAction { get; set; }
        public string ReturnUrl { get; set; }
    }
}