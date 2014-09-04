using NuGet.Jobs.Common;
using System;

namespace CatalogCollector.PackageRegistrationBlobs
{
    class Program
    {
        static void Main(string[] args)
        {
            var job = new Job();
            JobRunner.Run(job, args).Wait();
        }
    }
}
