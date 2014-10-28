using NuGet.Services.Metadata.Catalog;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetFeed
{
    public class CreateArgs
    {
        [ArgRequired]
        [ArgShortcut("i")]
        [ArgDescription("Path to nupkg folder")]
        public string Input { get; set; }

        [ArgRequired]
        [ArgShortcut("d")]
        [ArgDescription("Destination folder for catalog data")]
        public string Destination { get; set; }

        [ArgRequired]
        [ArgShortcut("F")]
        [ArgDescription("Delete the destination folder if it exists")]
        public bool Force { get; set; }
    }

    public partial class Commands
    {
        [ArgActionMethod]
        public void Create(CreateArgs args)
        {
            DirectoryInfo destinationDir = new DirectoryInfo(args.Destination);

            if (destinationDir.Exists && destinationDir.EnumerateFiles().Any())
            {
                if (args.Force)
                {
                    destinationDir.Delete(true);
                }
                else
                {
                    Console.WriteLine("The destination folder already has files. Use -F to force.");
                    Environment.Exit(1);
                }
            }

            destinationDir.Create();

            Config config = new Config("http://localhost:8000/", destinationDir.FullName);

            Queue<BuildStep> steps = new Queue<BuildStep>();

            Queue<string> nupkgs = new Queue<string>(Directory.GetFiles(args.Input, "*.nupkg"));

            steps.Enqueue(new CopyPackagesStep(config, nupkgs));
            steps.Enqueue(new CatalogStep(config, nupkgs));
            steps.Enqueue(new ResolverStep(config));
            steps.Enqueue(new InterceptStep(config));

            while(steps.Count > 0)
            {
                BuildStep step = steps.Dequeue();

                RunStep(step);

                step.Dispose();
            }
        }
    }
}
