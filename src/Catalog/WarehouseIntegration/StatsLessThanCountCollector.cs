using System;

namespace NuGet.Services.Metadata.Catalog.WarehouseIntegration
{
    public class StatsLessThanCountCollector : StatsCountCollector
    {
        DateTime _downloadTimeStamp;

        public StatsLessThanCountCollector(DateTime downloadTimeStamp)
        {
            _downloadTimeStamp = downloadTimeStamp;
        }

        protected override bool SelectItem(DateTime itemMinDownloadTimestamp, DateTime itemMaxDownloadTimestamp)
        {
            return (_downloadTimeStamp > itemMinDownloadTimestamp);
        }

        protected override bool SelectRow(DateTime rowDownloadTimestamp)
        {
            return (rowDownloadTimestamp < _downloadTimeStamp);
        }
    }
}
