
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class StatisticsPackagesReport
    {
        public IList<StatisticsPackagesItemViewModel> Rows { get; private set; }
        public string Total { get; set; }

        public IList<StatisticsDimension> Dimensions { get; private set; }
        public IList<StatisticsFact> Facts { get; set; }

        public ICollection<StatisticsPivot.TableEntry[]> Table { get; set; }
        public IEnumerable<string> Columns { get; set; }

        public DateTime? LastUpdatedUtc { get; set; }

        public string Id { get; set; }

        public StatisticsPackagesReport()
        {
            Total = String.Empty;
            Id = String.Empty;
            Columns = Enumerable.Empty<string>();
            Facts = new List<StatisticsFact>();
            Table = new List<StatisticsPivot.TableEntry[]>();
            Rows = new List<StatisticsPackagesItemViewModel>();
            Dimensions = new List<StatisticsDimension>();
        }
    }
}