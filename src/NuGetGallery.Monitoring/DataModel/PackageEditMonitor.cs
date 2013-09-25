using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace NuGetGallery.Monitoring
{
    public class PackageEditMonitor : SqlDatabaseMonitorBase
    {
        public PackageEditMonitor(string connectionString) : base(connectionString) { }
        public PackageEditMonitor(SqlConnectionStringBuilder connectionString) : base(connectionString) { }
        
        protected override async Task Invoke()
        {
            Connect(c =>
            {
                var failedEdits = c.Query(@"
                    SELECT pr.Id, p.Version, e.LastError, e.TriedCount
                    FROM PackageEdits e
                        INNER JOIN Packages p ON e.PackageKey = p.[Key]
                        INNER JOIN PackageRegistrations pr ON p.PackageRegistrationKey = pr.[Key]
                    WHERE e.TriedCount > 3");

                foreach (var failedEdit in failedEdits)
                {
                    Failure(
                        String.Format(
                            "Edit of {0} {1} failed after {2} tries. Error: {3}",
                            failedEdit.Id,
                            failedEdit.Version,
                            failedEdit.TriedCount,
                            failedEdit.LastError),
                        resource:
                            failedEdit.Id +
                            " " +
                            failedEdit.Version);
                }
            });
        }
    }
}
