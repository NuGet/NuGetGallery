using System;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
using NuGetGallery.Storage;

namespace NuGetGallery.Monitoring
{
    public class MonitoringHub
    {
        public StorageAccountHub Storage { get; private set; }

        public MonitoringHub(StorageAccountHub storage)
        {
            Storage = storage;
        }

        public virtual Task Start()
        {
            // Starts monitoring tasks.
            return Task.FromResult<object>(null);
        }
    }
}
