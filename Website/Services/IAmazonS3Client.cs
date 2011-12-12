using Amazon.S3;

namespace NuGetGallery
{
    public interface IAmazonS3Client
    {
        string BucketName { get; }
        AmazonS3 CreateInstance();
    }
}