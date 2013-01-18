using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using WebBackgrounder;

namespace NuGetGallery
{
    public class LuceneIndexingJob : Job
    {
        private readonly LuceneIndexingService _indexingService;

        public LuceneIndexingJob(TimeSpan frequence, Func<EntitiesContext> contextThunk, TimeSpan timeout)
            : base("Lucene", frequence, timeout)
        {
            _indexingService = new LuceneIndexingService(
                new PackageSource(contextThunk()),
                LuceneCommon.GetDirectory());

            // Updates the index synchronously first time job is created.
            // For startup code resiliency, we should handle exceptions for the database being down.
            try
            {
                _indexingService.UpdateIndex();
            }
            catch (SqlException e)
            {
                QuietlyLogException(e);
            }
            catch (DataException e)
            {
                QuietlyLogException(e);
            }
        }

        public override Task Execute()
        {
            return new Task(_indexingService.UpdateIndex);
        }

        private static void QuietlyLogException(Exception e)
        {
            try
            {
                Elmah.ErrorSignal.FromCurrentContext().Raise(e);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }
    }
}
