using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Helpers;

namespace NuGetGallery.Services
{
    public class TestFileReference : IFileReference
    {
        private byte[] _content;
        private int _openCount = 0;

        public int OpenCount { get { return _openCount; } }

        public bool HasChanged
        {
            get;
            private set;
        }

        public string ContentId
        {
            get;
            private set;
        }

        public TestFileReference(byte[] content, string contentId, bool hasChanged)
        {
            _content = content;
            ContentId = contentId;
            HasChanged = hasChanged;
        }

        public static TestFileReference CreateUnchanged(string content)
        {
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            string hash = Crypto.Hash(contentBytes);
            return new TestFileReference(new byte[0], hash, hasChanged: true);
        }

        public static TestFileReference CreateChanged(string content)
        {
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            string hash = Crypto.Hash(contentBytes);
            return new TestFileReference(contentBytes, hash, hasChanged: true);
        }

        public Stream OpenRead()
        {
            Interlocked.Increment(ref _openCount);
            return new MemoryStream(_content);
        }
    }
}
