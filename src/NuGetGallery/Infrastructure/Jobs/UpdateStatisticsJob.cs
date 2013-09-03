using System;
using System.Data.Entity;
using System.Threading.Tasks;
using WebBackgrounder;

namespace NuGetGallery.Jobs
{
    public class UpdateStatisticsJob : Job
    {
        private readonly Func<DbContext> _contextThunk;

        public UpdateStatisticsJob(TimeSpan interval, Func<DbContext> contextThunk, TimeSpan timeout)
            : base("Update Package Download Statistics", interval, timeout)
        {
            if (contextThunk == null)
            {
                throw new ArgumentNullException("contextThunk");
            }
            _contextThunk = contextThunk;
        }

        public override Task Execute()
        {
            return new Task(UpdateStats);
        }

        private void UpdateStats()
        {
            const string sql = "EXEC [dbo].[AggregateStatistics]";

            using (var context = _contextThunk())
            {
                context.Database.ExecuteSqlCommand(sql);
            }
        }
    }
}