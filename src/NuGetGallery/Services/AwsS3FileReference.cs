using System.Globalization;
using System.IO;
using Amazon.S3.IO;

namespace NuGetGallery
{
    public class AwsS3FileReference : IFileReference
    {
        private readonly S3FileInfo _file;

        public string ContentId
        {
            get { return _file.LastWriteTimeUtc.ToString("O", CultureInfo.CurrentCulture); }
        }

        public AwsS3FileReference(S3FileInfo file)
        {
            _file = file;
        }

        public Stream OpenRead()
        {
            return _file.OpenRead();
        }
    }
}