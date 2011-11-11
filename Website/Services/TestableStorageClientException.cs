using System;
using Microsoft.WindowsAzure.StorageClient;

namespace NuGetGallery
{
    public class TestableStorageClientException : Exception
    {
        public TestableStorageClientException()
        {
        }
        
        public TestableStorageClientException(StorageClientException ex)
        {
            ErrorCode = ex.ErrorCode;
        }

        public StorageErrorCode ErrorCode { get; set; }
    }
}