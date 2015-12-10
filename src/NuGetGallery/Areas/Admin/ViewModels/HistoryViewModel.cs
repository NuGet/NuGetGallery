using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class HistoryViewModel
    {
        public History History { get; set; }
        public string EditedBy { get; set; }
    }
}