using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class LocalFileReference : IFileReference
    {
        private FileInfo _file;

        public string FullName
        {
            get { return _file.FullName; }
        }

        public string Name
        {
            get { return _file.Name; }
        }

        public DateTime LastModifiedUtc
        {
            get { return _file.LastWriteTimeUtc; }
        }

        public string ContentId
        {
            get { return FullName + "@" + LastModifiedUtc.ToString(); }
        }

        public LocalFileReference(FileInfo file)
        {
            _file = file;
        }

        public Stream OpenRead()
        {
            return _file.Open(FileMode.Open);
        }
    }
}