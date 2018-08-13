using NuGet.Jobs;

namespace UpdateLicenseReports
{
    class Program
    {
        static void Main(string[] args)
        {
            var job = new UpdateLicenseReportsJob();
            JobRunner.Run(job, args).Wait();
        }
    }
}
