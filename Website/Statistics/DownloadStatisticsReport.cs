using System.Collections.Generic;
using System.Linq;
using Microsoft.Internal.Web.Utils;

namespace NuGetGallery.Statistics
{
    public class DownloadStatisticsReport
    {
        public int Total { get; set; }

        public IList<StatisticsDimension> Dimensions { get; private set; }
        public IList<StatisticsFact> Facts { get; private set; }
        public IList<StatisticsPivot.TableEntry[]> Table { get; private set; }
        public IList<string> Columns { get; private set; }

        public DownloadStatisticsReport() : this(Enumerable.Empty<StatisticsFact>())
        {
        }

        public DownloadStatisticsReport(IEnumerable<StatisticsFact> facts) 
            : this(facts, Enumerable.Empty<StatisticsDimension>(), Enumerable.Empty<string>(), Enumerable.Empty<StatisticsPivot.TableEntry[]>()) { }

        public DownloadStatisticsReport(IEnumerable<StatisticsFact> facts, IEnumerable<StatisticsDimension> dimensions, IEnumerable<string> columns, IEnumerable<StatisticsPivot.TableEntry[]> table)
        {
            Dimensions = new List<StatisticsDimension>(dimensions);
            Facts = new List<StatisticsFact>(facts);
            Columns = new List<string>(columns);
            Table = new List<StatisticsPivot.TableEntry[]>(table);
        }

        public override bool Equals(object obj)
        {
            var other = obj as DownloadStatisticsReport;
            return other != null &&
                Total == other.Total &&
                Enumerable.SequenceEqual(Dimensions, other.Dimensions) &&
                Enumerable.SequenceEqual(Facts, other.Facts) &&
                Enumerable.SequenceEqual(Table, other.Table) &&
                Enumerable.SequenceEqual(Columns, other.Columns);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(Total)
                .Add(Dimensions)
                .Add(Facts)
                .Add(Table)
                .Add(Columns)
                .CombinedHash;
        }
    }
}