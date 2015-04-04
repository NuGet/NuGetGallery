using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Store;

namespace NuGet.Indexing
{
    /// <summary>
    /// Rebuilds the index from scratch.
    /// </summary>
    public class FullBuildTask : IndexTask
    {
        public bool Force { get; set; }
        
        public override void Execute()
        {
            // Check for the frameworks list
            var frameworks = GetFrameworksList();
            
            DateTime before = DateTime.Now;

            if (Force && StorageAccount != null && !string.IsNullOrEmpty(Container))
            {
                AzureDirectoryManagement.ForceUnlockAzureDirectory(StorageAccount, Container);
            }

            Lucene.Net.Store.Directory directory = GetDirectory();

            PackageIndexing.RebuildIndex(SqlConnectionString, directory, frameworks, Log);

            DateTime after = DateTime.Now;
            Log.WriteLine("duration = {0} seconds", (after - before).TotalSeconds);
        }
    }
}
