using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public class SqlDatabaseSizeMonitor : SqlDatabaseMonitorBase
    {
        public static readonly decimal DefaultFailureThreshold = (4M /* Gb */ * 1024 /* Mb */);
        public static readonly decimal DefaultDegradedThreshold = (3.5M /* Gb */ * 1024 /* Mb */);

        public decimal DegradedThreshold { get; set; }
        public decimal FailureThreshold { get; set; }

        private const string TableSizeQuery = @"select    
      sys.objects.name, sum(reserved_page_count) * 8.0 / 1024
from    
      sys.dm_db_partition_stats, sys.objects
where    
      sys.dm_db_partition_stats.object_id = sys.objects.object_id

group by sys.objects.name";
        private const string DatabaseSizeQuery = @"SELECT SUM(reserved_page_count)*8.0/1024 FROM sys.dm_db_partition_stats";

        public SqlDatabaseSizeMonitor(string server, string database, string user, string password) : base(server, database, user, password) {
            DegradedThreshold = DefaultDegradedThreshold;
            FailureThreshold = DefaultFailureThreshold;
        }

        protected override Task Invoke()
        {
            return Connect(c =>
            {
                // Calculate overall database size
                // Source: http://social.msdn.microsoft.com/Forums/en-US/ssdsgetstarted/thread/a234d6e9-a9a4-4be3-9c35-4b9525491f1a
                decimal dbSizeInMB = (decimal)c.ExecuteScalar(DatabaseSizeQuery);

                QoS("Database Size in KB", success: true, value: (int)Math.Ceiling(dbSizeInMB * 1024));
                if (dbSizeInMB > FailureThreshold)
                {
                    Failure(String.Format("Database Size Extremely High. Size: {0}MB", dbSizeInMB));
                }
                else if (dbSizeInMB > DegradedThreshold)
                {
                    Degraded(String.Format("Database Size Very High. Size: {0}MB", dbSizeInMB));
                }
                else
                {
                    Success(String.Format("Database Size Acceptable. Size: {0}MB", dbSizeInMB));
                }
            });
        }
    }
}
