using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using WebBackgrounder;

namespace NuGetGallery
{
    public class LuceneIndexingJob : Job
    {
        private readonly IIndexingService _indexingService;

        public LuceneIndexingJob(TimeSpan frequence, Func<EntitiesContext> contextThunk, TimeSpan timeout, LuceneIndexLocation location, string storageConnectionString)
            : base("Lucene", frequence, timeout)
        {
            var context = contextThunk();

            if (location == LuceneIndexLocation.AzureStorage)
            {
                _indexingService = new AzureLuceneIndexingService(
                    new EntityRepository<Package>(context),
                    new EntityRepository<CuratedPackage>(context),
                    storageConnectionString,
                    null);            
            }
            else
            {
                _indexingService = new LuceneIndexingService(
                    new EntityRepository<Package>(context),
                    new EntityRepository<CuratedPackage>(context),
                    LuceneCommon.GetDirectory(location),
                    null);
            }

            // Updates the index synchronously first time job is created.
            // For startup code resiliency, we should handle exceptions for the database being down.
            try
            {
                _indexingService.UpdateIndex();
            }
            catch (SqlException e)
            {
                QuietLog.LogHandledException(e);
            }
            catch (DataException e)
            {
                QuietLog.LogHandledException(e);
            }
        }

        public override Task Execute()
        {
            return new Task(_indexingService.UpdateIndex);
        }
    }
}
