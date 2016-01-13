using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class IndexViewModel
    {
        public FilterResultsViewModel Filter { get; set; }
        public IEnumerable<IssueViewModel> Issues;
    }
}