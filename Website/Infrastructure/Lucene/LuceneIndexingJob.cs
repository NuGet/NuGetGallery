using System;
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
                new PackageSource(new EntitiesContext(readOnly: true)), // indexing service is always read-only
                LuceneCommon.GetDirectory());
            _indexingService.UpdateIndex();
        }

        public override Task Execute()
        {
            return new Task(_indexingService.UpdateIndex);
        }
    }
}