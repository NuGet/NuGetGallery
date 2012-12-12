using System;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery
{
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