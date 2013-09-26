using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using NuGetGallery.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery
{
    public class StorageMiniIntegrationTests
    {
        string constr = "UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://myhost:8888";
            

        [Fact]
        public void CanSaveAndLoadBlobsInANewContainer()
        {
            string newContainerName = "r" + new Random().Next();

            CloudBlobClient client = CloudStorageAccount.Parse(constr).CreateCloudBlobClient();
            var allContainers = client.ListContainers();
            var container = client.GetContainerReference(newContainerName);
            
            var service = new CloudBlobFileStorageService(
                new CloudBlobClientWrapper(constr),
                new Mock<IAppConfiguration>().Object);
            service.SaveFileAsync(newContainerName, "foo.nupkg", new MemoryStream(new byte[] { 0x42 })).Wait();

            Stream readBack = service.GetFileAsync(newContainerName, "foo.nupkg").Result;
            var x = new MemoryStream(1);
            readBack.CopyTo(x);
            Assert.Equal(new byte[] { 0x42 }, x.GetBuffer());
        }

        [Fact]
        public void CanComputeUrlsForBlobsDoesntMatterIfTheyExistYet()
        {
            string newContainerName = "r" + new Random().Next();
            string newFileName = "f" + new Random().Next();

            var service = new CloudBlobFileStorageService(
                new CloudBlobClientWrapper(constr),
                new Mock<IAppConfiguration>().Object);

            var uri = service.GetBlobUri(newContainerName, newFileName, requireHttps: false);
        }
    }
}
