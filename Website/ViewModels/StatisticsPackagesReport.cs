
using System.Collections.Generic;

namespace NuGetGallery
{
    public class StatisticsPackagesReport
    {
        private List<StatisticsPackagesItemViewModel> _rows = new List<StatisticsPackagesItemViewModel>();

        public IList<StatisticsPackagesItemViewModel> Rows { get { return _rows; } }
        public int Total { get; set; }
    }
}