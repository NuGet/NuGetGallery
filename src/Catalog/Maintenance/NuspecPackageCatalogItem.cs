using System.IO;
using System.Xml.Linq;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class NuspecPackageCatalogItem : PackageCatalogItem
    {
        private XDocument _nuspec;

        public NuspecPackageCatalogItem(string path)
        {
            Path = path;
        }

        public NuspecPackageCatalogItem(string path, XDocument nuspec)
        {
            Path = path;
            _nuspec = nuspec;
        }

        public string Path
        {
            get;
            private set;
        }

        protected override XDocument GetNuspec()
        {
            if (_nuspec == null)
            {
                lock(this)
                {
                    if (_nuspec == null)
                    {
                        using (StreamReader reader = new StreamReader(Path))
                        {
                            return XDocument.Load(reader);
                        }
                    }
                }
            }

            return _nuspec;
        }
    }
}
