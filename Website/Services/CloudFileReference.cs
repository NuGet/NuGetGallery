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
        private MemoryStream _stream;

        public string FullName
        {
            get { return _blob.Uri.AbsoluteUri; }
        }

        public string Name
        {
            get { return _blob.Name; }
        }

        public DateTime LastModifiedUtc
        {
            get { return _blob.LastModifiedUtc; }
        }

        public string ContentId
        {
            get { return _blob.ETag; }
        }

        public CloudFileReference(ISimpleCloudBlob blob, MemoryStream stream)
        {
            _blob = blob;
            _stream = stream;
        }

        public Stream OpenRead()
        {
            _stream.Seek(0, SeekOrigin.Begin);
            return _stream;
        }
    }
}
