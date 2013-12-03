using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery.Storage
{
    public class StorageHub
    {
        public StorageAccountHub Primary { get; private set; }
        public StorageAccountHub Backup { get; private set; }
        
        public StorageHub(CloudStorageAccount primary, CloudStorageAccount backup)
            : this(new StorageAccountHub(primary), new StorageAccountHub(backup)) { }

        public StorageHub(StorageAccountHub primary, StorageAccountHub backup)
        {
            Primary = primary;
            Backup = backup;
        }
    }
}
