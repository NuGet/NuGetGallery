using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.Search
{
    public class IndexPackageUpdatesTask : IndexTask
    {
        public override void ExecuteCommand()
        {
            Lucene.Net.Store.Directory directory = GetDirectory();
            PackageRanking packageRanking = new WarehousePackageRanking(StorageAccount);
            PackageIndexing.ApplyPackageEdits(ConnectionString.ToString(), directory, packageRanking);
        }
    }
}
