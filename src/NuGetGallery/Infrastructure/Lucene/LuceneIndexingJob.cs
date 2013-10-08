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
        private readonly Action _updateIndex;

        public LuceneIndexingJob(TimeSpan frequence, Func<EntitiesContext> contextThunk, TimeSpan timeout, LuceneIndexLocation location)
            : base("Lucene", frequence, timeout)
        {
            _updateIndex = () =>
            {
                using (var context = contextThunk())
                {
                    var indexingService = new LuceneIndexingService(
                        new EntityRepository<Package>(context),
                        new EntityRepository<CuratedPackage>(context),
                        LuceneCommon.GetDirectory(location),
                        null);
                    indexingService.UpdateIndex();
                }
            };

            // Updates the index synchronously first time job is created.
            // For startup code resiliency, we should handle exceptions for the database being down.
            try
            {
                _updateIndex();
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
            return new Task(_updateIndex);
        }
    }
}
