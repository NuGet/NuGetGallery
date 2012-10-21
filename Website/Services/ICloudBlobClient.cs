namespace NuGetGallery
{
    public interface ICloudBlobClient
    {
        ICloudBlobContainer GetContainerReference(string containerAddress);
    }
}