using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetGallery.Monitoring.Tables
{
    // Marker interface
    public interface IMonitoringTableEntry : ITableEntity
    {
    }
}
