using System;
using System.Net;
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
            if (ex.RequestInformation != null)
            {
                HttpStatusCode = (HttpStatusCode)ex.RequestInformation.HttpStatusCode;
            }
        }

        public HttpStatusCode? HttpStatusCode { get; set; }
    }
}