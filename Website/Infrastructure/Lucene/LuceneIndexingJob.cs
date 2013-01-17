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

        public LuceneIndexingJob(TimeSpan frequence, TimeSpan timeout)
            : base("Lucene", frequence, timeout)
        {
            _indexingService = new LuceneIndexingService(
                new PackageSource(new EntitiesContext()),
                LuceneCommon.GetDirectory());

            // Updates the index synchronously first time job is created.
            // For startup code resiliency, we should handle exceptions for the database being down.
            try
            {
                _indexingService.UpdateIndex();
            }
            catch (Exception ex)
            {
                if (!(ex is SqlException || ex is DataException))
                {
                    throw; // unexpected exceptions
                }

                Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
            }
        }

        public override Task Execute()
        {
            return new Task(_indexingService.UpdateIndex);
        }
    }
}