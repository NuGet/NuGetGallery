using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class NuspecPackageCatalogItem : PackageCatalogItem
    {
        private XDocument _nuspec;
        private DateTime? _published;
        private IEnumerable<PackageEntry> _entries;

        public NuspecPackageCatalogItem(string path)
        {
            Path = path;
            _published = null;
        }

        public NuspecPackageCatalogItem(XDocument nuspec, DateTime? published = null, IEnumerable<PackageEntry> entries = null)
        {
            _nuspec = nuspec;
            _published = published;
            _entries = entries;
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

        protected override DateTime? GetPublished()
        {
            return _published;
        }

        protected override IEnumerable<PackageEntry> GetEntries()
        {
            return _entries;
        }
    }
}
