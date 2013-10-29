using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.Search
{
    public abstract class IndexTask : DatabaseTask
    {
        [Option("The connection string to the storage server", AltName = "st")]
        public CloudStorageAccount StorageAccount { get; set; }

        [Option("The Blob Storage Container", AltName = "cont")]
        public string Container { get; set; }

        [Option("The file system folder", AltName = "folder")]
        public string Folder { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (Folder != null && Container != null)
            {
                throw new CommandLineException("Either specify a Folder or a Container not both");
            }
            if (Folder != null)
            {
                //  using file system - make sure there is no confusion about that - force the StorageAccount argument to be null

                if (StorageAccount != null)
                {
                    throw new CommandLineException("Specifying a folder means you shouldn't specify a StorageAccount");
                }
            }
            else
            {
                //  using Blob Storage so we must have both the Container and the StorageAccount

                if (Container == null || StorageAccount == null)
                {
                    throw new CommandLineException("You must specify a StorageAccount with a Container");
                }
            }
        }

        protected Lucene.Net.Store.Directory GetDirectory()
        {
            Lucene.Net.Store.Directory directory = null;

            if (string.IsNullOrEmpty(Container))
            {
                directory = new AzureDirectory(StorageAccount, Container, new RAMDirectory());
            }
            else if (string.IsNullOrEmpty(Folder))
            {
                directory = new SimpleFSDirectory(new DirectoryInfo(Folder));
            }

            if (directory == null)
            {
                throw new Exception("You must specify either a folder or storage");
            }

            return directory;
        }
    }
}
