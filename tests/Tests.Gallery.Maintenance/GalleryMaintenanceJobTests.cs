using Gallery.Maintenance;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Tests.Gallery.Maintenance
{
    public class GalleryMaintenanceJobTests
    {
        [Fact]
        public void GetMaintenanceTasks_CreatesTasksAndDoesNotThrow()
        {
            var job = CreateJob();

            var tasks = job.GetMaintenanceTasks();

            Assert.NotEmpty(tasks);
        }

        private Job CreateJob()
        {
            var job = new Job();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<Job>();
            job.SetLogger(loggerFactory, logger);
            return job;
        }
    }
}
