using System;

namespace NuGet.Services.Metadata.Catalog.WarehouseIntegration
{
    public class StatsGreaterThanCountCollector : StatsCountCollector
    {
        DateTime _minDownloadTimeStamp;

        public StatsGreaterThanCountCollector(DateTime minDownloadTimeStamp)
        {
            _minDownloadTimeStamp = minDownloadTimeStamp;
        }

        protected override bool SelectItem(DateTime itemMinDownloadTimestamp, DateTime itemMaxDownloadTimestamp)
        {
            return (_minDownloadTimeStamp < itemMaxDownloadTimestamp);
        }

        protected override bool SelectRow(DateTime rowDownloadTimestamp)
        {
            return (rowDownloadTimestamp > _minDownloadTimeStamp);
        }
    }
}
