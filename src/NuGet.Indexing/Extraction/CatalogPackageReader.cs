using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;

namespace NuGet.Indexing
{
    public class CatalogPackageReader 
        : PackageReaderBase, IDisposable
    {
        private readonly JObject _catalogItem;
        private readonly CatalogNuspecReader _catalogNuspecReader;

        public CatalogPackageReader(JObject catalogItem) : base(DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance)
        {
            _catalogItem = catalogItem;
            _catalogNuspecReader = new CatalogNuspecReader(_catalogItem);
        }

        public override Stream GetStream(string path)
        {
            throw new NotSupportedException();
        }

        public override Stream GetNuspec()
        {
            return _catalogNuspecReader.NuspecStream;
        }
        
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

        public override IEnumerable<string> GetFiles(string folder)
        {
            return GetFiles().Where(f => f.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
        }

        public override IEnumerable<string> CopyFiles(string destination, IEnumerable<string> packageFiles, ExtractPackageFileDelegate extractFile,
            Common.ILogger logger, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _catalogNuspecReader.Dispose();
            }
        }

        public override Task<PrimarySignature> GetPrimarySignatureAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> IsSignedAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task ValidateIntegrityAsync(SignatureContent signatureContent, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task<byte[]> GetArchiveHashAsync(HashAlgorithmName hashAlgorithm, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}