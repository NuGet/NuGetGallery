using System;
using System.Threading.Tasks;
using WebBackgrounder;

namespace NuGetGallery
{
    public class LuceneIndexingJob : Job
    {
        private readonly LuceneIndexingService indexingService;

        public LuceneIndexingJob(TimeSpan frequence, TimeSpan timeout)
            : base("Lucene", frequence, timeout)
        {
            indexingService = new LuceneIndexingService();
            indexingService.UpdateIndex();
        }
        
        public override Task Execute()
        {
            return new Task(indexingService.UpdateIndex);
        }
    }
}