using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    public interface ICloudBlobContainer
    {
        Task CreateIfNotExistAsync();
        Task SetPermissionsAsync(BlobContainerPermissions permissions);
        Task<ISimpleCloudBlob> GetBlobReferenceAsync(string blobAddressUri);
    }
}