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
        private readonly LuceneIndexingService _indexingService;

        public LuceneIndexingJob(TimeSpan frequence, TimeSpan timeout, LuceneIndexingService indexingService)
            : base("Lucene", frequence, timeout)
        {
            _indexingService = indexingService;

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
