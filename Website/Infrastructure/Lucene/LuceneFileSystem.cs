using System;
using System.IO;
using Lucene.Net.Store;

namespace NuGetGallery
{
    internal sealed class LuceneFileSystem : SimpleFSDirectory, IDisposable
    {
        public LuceneFileSystem(string path)
            : base(new DirectoryInfo(path))
        {

        }

        public void Dispose()
        {
            base.Close();
        }
    }
}