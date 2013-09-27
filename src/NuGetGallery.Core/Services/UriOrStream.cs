using System;
using System.IO;

namespace NuGetGallery
{
    public struct UriOrStream
    {
        public static UriOrStream NotFound { get; private set; }

        private Uri _uri;
        private Stream _stream;

        public Uri Uri { get { return _uri; } }
        public Stream Stream { get { return _stream; } }

        public UriOrStream(Uri uri)
        {
            _uri = uri;
            _stream = null;
        }

        public UriOrStream(Stream stream)
        {
            _uri = null;
            _stream = stream;
        }
    }
}
