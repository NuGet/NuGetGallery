using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class StorageWriteLock
    {
        string _connectionString;
        string _container;
        string _leaseId;
        TimeSpan _duration;
        CloudBlockBlob _writeLockBlob;

        public StorageWriteLock(string connectionString, string container, int seconds = 30)
        {
            _connectionString = connectionString;
            _container = container;
            _duration = TimeSpan.FromSeconds(seconds < 15 ? 15 : seconds);
        }

        public async Task AquireAsync()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_connectionString);
            CloudBlobClient client = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(_container);
            _writeLockBlob = container.GetBlockBlobReference("write.lock");

            _leaseId = await AquireAsync(_writeLockBlob, _duration);
        }
        public async Task ReleaseAsync()
        {
            await _writeLockBlob.ReleaseLeaseAsync(AccessCondition.GenerateLeaseCondition(_leaseId));
        }

        static async Task<string> AquireAsync(CloudBlockBlob blob, TimeSpan timeStamp)
        {
            bool retry = false;
            do
            {
                try
                {
                    return await blob.AcquireLeaseAsync(TimeSpan.FromSeconds(15), null);
                }
                catch (StorageException e)
                {
                    WebException webException = e.InnerException as WebException;
                    if (webException != null)
                    {
                        HttpStatusCode statusCode = ((HttpWebResponse)webException.Response).StatusCode;
                        if (statusCode == HttpStatusCode.Conflict)
                        {
                            Thread.Sleep(500);
                            retry = true;
                        }
                        else if (statusCode == HttpStatusCode.NotFound)
                        {
                            blob.UploadText(string.Empty);
                            retry = true;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            while (retry);

            return null;
        }
    }
}
