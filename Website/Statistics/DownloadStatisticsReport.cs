using System.Collections.Generic;

namespace NuGetGallery.Statistics
{
    public class DownloadStatisticsReport
    {
        private List<StatisticsReportRow> _rows = new List<StatisticsReportRow>();
        private List<StatisticsDimension> _dimensions = new List<StatisticsDimension>();

        public IList<StatisticsReportRow> Rows { get { return _rows; } }
        public int Total { get; set; }

        public IList<StatisticsDimension> Dimensions { get { return _dimensions; } }
        public IList<StatisticsFact> Facts { get; set; }

        public ICollection<StatisticsPivot.TableEntry[]> Table { get; set; }
        public IEnumerable<string> Columns { get; set; }
    }
}