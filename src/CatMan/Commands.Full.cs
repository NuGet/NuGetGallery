using PowerArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatMan
{
    public class FullArgs
    {
        [ArgShortcut("nupkgs")]
        [ArgDescription("Path to folder filled with nupkgs and/or nuspecs")]
        public string NuPkgFolder { get; set; }

        [ArgShortcut("base")]
        [ArgDescription("Base address for the generated catalog data")]
        [DefaultValue("http://localhost:3333/")]
        public string BaseAddress { get; set; }

        [ArgRequired]
        [ArgShortcut("d")]
        [ArgDescription("Destination root folder")]
        public string OutputFolder { get; set; }

        // TODO: add clean rebuild vs incremental flag
    }

    public partial class Commands
    {
        [ArgActionMethod]
        public void Full(FullArgs args)
        {
            RebuildArgs rebuildArgs = new RebuildArgs();
            rebuildArgs.BaseAddress = args.BaseAddress.Trim('/') + "/catalog";
            string catalogFolder = Path.Combine(args.OutputFolder, "catalog");
            rebuildArgs.CatalogFolder = catalogFolder;
            rebuildArgs.NuPkgFolder = args.NuPkgFolder;

            Rebuild(rebuildArgs);

            // TODO: add the resolver
        }
    }
}
