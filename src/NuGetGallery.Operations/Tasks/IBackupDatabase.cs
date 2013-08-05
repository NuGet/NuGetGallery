using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations
{
    // this interface allows backup Jobs to combine Backup Task with Check Status tasks
    public interface IBackupDatabase
    {
        SqlConnectionStringBuilder ConnectionString { get; }
        string BackupName { get; }
        bool SkippingBackup { get; }
    }
}
