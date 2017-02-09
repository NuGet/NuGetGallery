using System.Threading.Tasks;
using Lucene.Net.Store;

namespace NuGet.Indexing.IndexDirectoryProvider
{
    /// <summary>
    /// Stores a directory and an index container name but does not reload them or provide a synchronizer.
    /// </summary>
    public class LocalIndexDirectoryProvider : IIndexDirectoryProvider
    {
        private readonly Directory _directory;
        private readonly string _indexContainerName;

        public LocalIndexDirectoryProvider(Directory directory, string indexContainerName)
        {
            _directory = directory;
            _indexContainerName = indexContainerName;
        }

        public Directory GetDirectory()
        {
            return _directory;
        }

        public string GetIndexContainerName()
        {
            return _indexContainerName;
        }

        public AzureDirectorySynchronizer GetSynchronizer()
        {
            return null;
        }

        public bool Reload(IndexingConfiguration config)
        {
            return false;
        }
    }
}
