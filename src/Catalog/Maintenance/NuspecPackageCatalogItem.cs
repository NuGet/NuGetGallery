using System;
using System.IO;
using System.Xml.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class NuspecPackageCatalogItem : PackageCatalogItem
    {
        private readonly string _fullName;
        private XDocument _nuspec;

        public NuspecPackageCatalogItem(string fullName)
        {
            _fullName = fullName;
        }

        public NuspecPackageCatalogItem(string fullName, XDocument nuspec)
        {
            _fullName = fullName;
            _nuspec = nuspec;
        }

        protected override XDocument GetNuspec()
        {
            if (_nuspec == null)
            {
                lock(this)
                {
                    if (_nuspec == null)
                    {
                        using (StreamReader reader = new StreamReader(_fullName))
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
