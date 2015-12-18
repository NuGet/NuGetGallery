using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace NuGet.Indexing
{
    public class CatalogPackageReader : PackageReaderBase
    {
        private readonly JObject _catalogItem;

        public CatalogPackageReader(JObject catalogItem) : base(DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance)
        {
            _catalogItem = catalogItem;
        }

        public override Stream GetStream(string path)
        {
            throw new NotSupportedException();
        }

        protected override NuspecReader Nuspec => new CatalogNuspecReader(_catalogItem);

        public override IEnumerable<string> GetFiles()
        {
            var array = _catalogItem.GetJArray("packageEntries");
            if (array == null)
            {
                yield break;
            }

            foreach (var entry in array)
            {
                yield return (string)entry["fullName"];
            }
        }

        protected override IEnumerable<string> GetFiles(string folder)
        {
            return GetFiles().Where(f => f.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
        }
    }
}