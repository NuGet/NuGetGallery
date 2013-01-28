using System;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable", Justification = "This is for unit tests only.")]
    public class TestableStorageClientException : Exception
    {
        public TestableStorageClientException()
        {
        }

        public TestableStorageClientException(StorageException ex)
        {
            ErrorCode = ex.RequestInformation.ExtendedErrorInformation.ErrorCode;
        }

        public string ErrorCode { get; set; }
    }
}