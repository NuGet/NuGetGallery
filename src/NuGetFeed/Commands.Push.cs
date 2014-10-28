using NuGet.Services.Metadata.Catalog;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetFeed
{
    public class PushArgs
    {
        [ArgRequired]
        [ArgShortcut("i")]
        [ArgDescription("Nupkg file path")]
        public string Nupkg { get; set; }

        [ArgRequired]
        [ArgShortcut("d")]
        [ArgDescription("Root destination folder")]
        public string Destination { get; set; }
    }

    public partial class Commands
    {
        [ArgActionMethod]
        public void Push(PushArgs args)
        {
            DirectoryInfo destinationDir = new DirectoryInfo(args.Destination);

            if (!destinationDir.Exists)
            {
                Console.WriteLine("The destination folder does not exist.");
                Environment.Exit(1);
            }

            FileInfo nupkg = new FileInfo(args.Nupkg);
            if (!nupkg.Exists)
            {
                Console.WriteLine("The given nupkg does not exist.");
                Environment.Exit(1);
            }

            Config config = new Config("http://localhost:8000/", destinationDir.FullName);

            Queue<BuildStep> steps = new Queue<BuildStep>();

            Queue<string> nupkgs = new Queue<string>();
            nupkgs.Enqueue(nupkg.FullName);

            // start the cursor before any operators take place
            CollectorCursor cursor = new CollectorCursor(DateTime.UtcNow);

            steps.Enqueue(new CopyPackagesStep(config, nupkgs));
            steps.Enqueue(new CatalogStep(config, nupkgs));
            steps.Enqueue(new ResolverStep(config, cursor));

            while (steps.Count > 0)
            {
                BuildStep step = steps.Dequeue();

                RunStep(step);

                step.Dispose();
            }
        }
    }
}
