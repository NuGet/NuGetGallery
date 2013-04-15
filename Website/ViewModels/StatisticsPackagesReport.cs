
using System.Collections.Generic;

namespace NuGetGallery
{
    public class StatisticsPackagesReport
    {
        private List<StatisticsPackagesItemViewModel> _rows = new List<StatisticsPackagesItemViewModel>();
        private List<StatisticsDimension> _dimensions = new List<StatisticsDimension>();

        public IList<StatisticsPackagesItemViewModel> Rows { get { return _rows; } }
        public int Total { get; set; }

        public IList<StatisticsDimension> Dimensions { get { return _dimensions; } }
        public IList<StatisticsFact> Facts { get; set; }

        public ICollection<StatisticsPivot.TableEntry[]> Table { get; set; }
        public IEnumerable<string> Columns { get; set; }
    }
}