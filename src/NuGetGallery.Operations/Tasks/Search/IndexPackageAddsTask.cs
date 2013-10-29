using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.Search
{
    public class IndexPackageAddsTask : IndexTask
    {
        [Option("When using blob storage force unlock the index for write", AltName = "force")]
        public bool Force { get; set; }

        [Option("The clear the current index before starting", AltName = "clear")]
        public bool Clear { get; set; }

        public override void ExecuteCommand()
        {
            if (Force && StorageAccount != null && Container != null)
            {
                AzureDirectoryManagement.ForceUnlockAzureDirectory(StorageAccount, Container);
            }

            Lucene.Net.Store.Directory directory = GetDirectory();
            
            if (Clear)
            {
                PackageIndexing.CreateNewEmptyIndex(directory);
            }

            PackageRanking packageRanking = new WarehousePackageRanking(StorageAccount);
            PackageIndexing.IncrementallyUpdateIndex(ConnectionString.ToString(), directory, packageRanking);
        }
    }
}
