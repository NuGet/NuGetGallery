using System.Threading.Tasks;
using Lucene.Net.Store;

namespace NuGet.Indexing.IndexDirectoryProvider
{
    public interface IIndexDirectoryProvider
    {
        Directory GetDirectory();
        string GetIndexContainerName();
        AzureDirectorySynchronizer GetSynchronizer();

        /// <summary>
        /// Reloads the index directory.
        /// </summary>
        /// <param name="config">Configuration to use.</param>
        /// <returns>Returns true if the index directory has changed.</returns>
        bool Reload(IndexingConfiguration config);
    }
}
