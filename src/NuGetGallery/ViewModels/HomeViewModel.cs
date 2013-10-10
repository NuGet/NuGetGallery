using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.ViewModels
{
    public class HomeViewModel
    {
        public IHtmlString Announcement { get; set; }
        public IHtmlString About { get; set; }
    }
}