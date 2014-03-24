using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGetGallery.Configuration;
using WebBackgrounder;
namespace NuGetGallery
{
    public interface IIndexingService
    {
        Task<DateTime?> GetLastWriteTime();
        void UpdateIndex();
        void UpdateIndex(bool forceRefresh);
        void UpdatePackage(Package package);

        Task<int> GetDocumentCount();
        Task<long> GetIndexSizeInBytes();

        void RegisterBackgroundJobs(IList<IJob> jobs, IAppConfiguration configuration);

        string IndexPath { get; }

        bool IsLocal { get; }
    }
}