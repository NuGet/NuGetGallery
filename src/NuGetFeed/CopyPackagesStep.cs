using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetFeed
{
    public class CopyPackagesStep : BuildStep
    {
        private IEnumerable<string> _nupkgs;

        public CopyPackagesStep(Config config, IEnumerable<string> nupkgs)
            : base(config, "CopyPackages")
        {
            _nupkgs = new Queue<string>(nupkgs);
        }

        protected override void RunCore()
        {
            DirectoryInfo dir = new DirectoryInfo(Path.Combine(Config.Packages.LocalFolder.FullName, "packages"));

            dir.Create();

            int i = 0;
            int total = _nupkgs.Count();

            foreach(var file in _nupkgs)
            {
                FileInfo src = new FileInfo(file);
                FileInfo dest = new FileInfo(Path.Combine(dir.FullName, src.Name));

                if (!src.Exists)
                {
                    LogFatalError("unable to find: " + src.FullName);
                    return;
                }

                File.Copy(src.FullName, dest.FullName, true);

                if (i > 0 && i % 1000 == 0)
                {
                    ProgressUpdate(i, total);
                }
            }
        }
    }
}
