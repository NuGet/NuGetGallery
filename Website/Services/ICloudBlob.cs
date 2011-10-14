using System;
using System.IO;

namespace NuGetGallery
{
    public interface ICloudBlob
    {
        Uri Uri { get; }

        void DeleteIfExists();
        void UploadFromStream(Stream packageFile);
    }
}
