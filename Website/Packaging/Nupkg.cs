using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using NuGet;

namespace NuGetGallery
{
    public class Nupkg : INupkg
    {
        private Stream _packageStream;

        public Nupkg(Stream packageStream)
        {
            _packageStream = packageStream;
        }

        public IPackageMetadata Metadata
        {
            get { throw new NotImplementedException(); }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetFiles()
        {
            throw new NotImplementedException();
        }

        public Stream GetStream()
        {
            throw new NotImplementedException();
        }

        public Stream GetCheckedFileStream(string filePath, int maxSize)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<FrameworkName> GetSupportedFrameworks()
        {
            throw new NotImplementedException();
        }
    }
}