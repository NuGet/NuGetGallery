using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Backend
{
    public enum KnownSqlServer
    {
        Primary,
        Warehouse
    }

    public enum KnownStorageAccount
    {
        Primary,
        Backup,
        Diagnostics
    }
}
