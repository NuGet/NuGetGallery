using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NuGetGallery
{
    public class CloudFileReference : IFileReference
    {
        private ISimpleCloudBlob _blob;
        private Stream _stream;

        public string ContentId
        {
            get { return _blob.ETag; }
        }

        public CloudFileReference(ISimpleCloudBlob blob, Stream stream)
        {
            _blob = blob;
            _stream = stream;
        }

        public Stream OpenRead()
        {
            return _stream;
        }
    }
}
