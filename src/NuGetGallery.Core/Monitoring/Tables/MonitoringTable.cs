using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetGallery.Monitoring.Tables
{
    public class MonitoringTable<TEntity> where TEntity : IMonitoringTableEntry
    {
        private CloudTable _table;

        public MonitoringTable(CloudTable table)
        {
            _table = table;
        }

        public Task Upsert(TEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
