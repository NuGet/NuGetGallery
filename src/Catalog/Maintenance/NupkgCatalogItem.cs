using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Maintenance;
using System.IO;
using System.Xml.Linq;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class NupkgCatalogItem : PackageCatalogItem
    {
        private readonly string _fullName;
        private XDocument _nuspec;

        public NupkgCatalogItem(string fullName)
        {
            _fullName = fullName;
        }

        protected override XDocument GetNuspec()
        {
            if (_nuspec == null)
            {
                lock (this)
                {
                    if (_nuspec == null)
                    {
                        using (StreamReader reader = new StreamReader(_fullName))
                        {
                            var package = Utils.GetPackage(reader.BaseStream);
                            _nuspec = Utils.GetNuspec(package);
                        }
                    }
                }
            }

            return _nuspec;
        }
    }
}
