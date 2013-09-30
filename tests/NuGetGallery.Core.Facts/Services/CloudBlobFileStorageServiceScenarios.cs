using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Services
{
    public class CloudBlobFileStorageServiceScenarios
    {
        string constr = "UseDevelopmentStorage=true";
        //string constr = "UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://myhost:8888"; - can be used for fiddler debugging - set myhost in your hosts file

        [Fact]
        public async Task CanSaveAndLoadBlobsInANewContainer()
        {
            string newContainerName = "r" + new Random().Next();

            CloudBlobClient client = CloudStorageAccount.Parse(constr).CreateCloudBlobClient();

            var service = new CloudBlobFileStorageService(
                new CloudBlobClientWrapper(constr),
                _ => true);

            await service.SaveFileAsync(newContainerName, "foo.nupkg", new MemoryStream(new byte[] { 0x42 }), "application/zip");

            Stream readBack = service.GetFileAsync(newContainerName, "foo.nupkg").Result;
            var x = new MemoryStream(1);
            readBack.CopyTo(x);
            Assert.Equal(new byte[] { 0x42 }, x.GetBuffer());
        }

        [Theory]
        [InlineData(true, BlobContainerPublicAccessType.Blob)]
        [InlineData(false, BlobContainerPublicAccessType.Off)]
        public async Task CreatesContainersWithCorrectPermissions_PerPolicy(
            bool isPublic, 
            BlobContainerPublicAccessType expectedAccessLevel)
        {
            string newContainerName = "r" + new Random().Next();

            CloudBlobClient client = CloudStorageAccount.Parse(constr).CreateCloudBlobClient();

            var service = new CloudBlobFileStorageService(
                new CloudBlobClientWrapper(constr),
                _ => isPublic);

            await service.SaveFileAsync(newContainerName, "foo.nupkg", new MemoryStream(new byte[] { 0x42 }), "application/zip");

            var container = client.GetContainerReference(newContainerName);
            var permissions = container.GetPermissions();
            Assert.Equal(expectedAccessLevel, permissions.PublicAccess);
            Assert.Empty(permissions.SharedAccessPolicies);
        }

        [Fact]
        public async Task BlobExistsInANewContainer()
        {
            string newContainerName = "r" + new Random().Next();

            CloudBlobClient client = CloudStorageAccount.Parse(constr).CreateCloudBlobClient();

            var service = new CloudBlobFileStorageService(
                new CloudBlobClientWrapper(constr),
                _ => true);

            bool fooExisted = await service.FileExistsAsync(newContainerName, "foo.nupkg");
            await service.SaveFileAsync(newContainerName, "foo.nupkg", new MemoryStream(new byte[] { 0x42 }), "application/zip");
            bool fooExists = await service.FileExistsAsync(newContainerName, "foo.nupkg");
            bool barExists = await service.FileExistsAsync(newContainerName, "bar.nupkg");
            Assert.True(fooExists);
            Assert.False(barExists);
            Assert.False(fooExisted);
        }

        [Fact]
        public async Task DeleteBlobInANewContainer()
        {
            string newContainerName = "r" + new Random().Next();

            CloudBlobClient client = CloudStorageAccount.Parse(constr).CreateCloudBlobClient();

            var service = new CloudBlobFileStorageService(
                new CloudBlobClientWrapper(constr),
                _ => true);

            await service.SaveFileAsync(newContainerName, "foo.nupkg", new MemoryStream(new byte[] { 0x42 }), "application/zip");
            await service.DeleteFileAsync(newContainerName, "foo.nupkg");

            try
            {
                Stream readBack = service.GetFileAsync(newContainerName, "foo.nupkg").Result;
            }
            catch (Exception e)
            {
                Assert.IsType<StorageException>(e);
            }
        }

        [Fact]
        public void CanComputeUrlsForBlobs_DoesntMatterIfTheyExistYet()
        {
            string newContainerName = "r" + new Random().Next();
            string newFileName = "f" + new Random().Next();

            var service = new CloudBlobFileStorageService(
                new CloudBlobClientWrapper(constr),
                _ => true);

            var uri = service.GetDownloadUriOrStream(newContainerName, newFileName).Uri;
            Assert.NotNull(uri);
        }

        [Fact]
        public async Task CanCopyBlobsInANewContainer()
        {
            string newContainerName1 = "r" + new Random().Next();
            string newContainerName2 = "s" + new Random().Next();

            CloudBlobClient client = CloudStorageAccount.Parse(constr).CreateCloudBlobClient();

            var service = new CloudBlobFileStorageService(
                new CloudBlobClientWrapper(constr),
                _ => true);

            await service.SaveFileAsync(newContainerName1, "source.nupkg", new MemoryStream(new byte[] { 0x42 }), "application/zip");
            await service.BeginCopyAsync(newContainerName1, "source.nupkg", newContainerName2, "dest.nupkg");
            await service.WaitForCopyCompleteAsync(newContainerName2, "dest.nupkg");

            Stream readBack = service.GetFileAsync(newContainerName2, "dest.nupkg").Result;
            var x = new MemoryStream(1);
            readBack.CopyTo(x);
            Assert.Equal(new byte[] { 0x42 }, x.GetBuffer());
        }
    }
}
