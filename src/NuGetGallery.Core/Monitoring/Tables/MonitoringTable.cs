using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;

namespace NuGetGallery.Monitoring.Tables
{
    public class MonitoringTable<TEntity> where TEntity : ITableEntity
    {
        private CloudTable _table;

        public MonitoringTable(CloudTable table)
        {
            _table = table;
        }

        /// <summary>
        /// Inserts the specified value or replaces an existing value if one exists with the same keys
        /// </summary>
        public Task Upsert(TEntity entity)
        {
            return _table.SafeExecute(t => t.ExecuteAsync(TableOperation.InsertOrReplace(entity)));
        }

        /// <summary>
        /// Inserts the specified value and silently fails, ignoring the new value, if there is already an entry with the same keys
        /// </summary>
        public async Task InsertOrIgnoreDuplicate(TEntity entity)
        {
            try
            {
                await _table.SafeExecute(t => t.ExecuteAsync(TableOperation.Insert(entity)));
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.ExtendedErrorInformation.ErrorCode == TableErrorCodeStrings.EntityAlreadyExists)
                {
                    return;
                }
                throw;
            }
        }
    }
}
