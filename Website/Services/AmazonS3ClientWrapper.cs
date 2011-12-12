using Amazon.S3;

namespace NuGetGallery
{
    public class AmazonS3ClientWrapper : IAmazonS3Client
    {
        private readonly IConfiguration configuration;
        private readonly string accessKeyId = "";
        private readonly string accessSecret = "";
        private readonly string bucketName = "";

        public AmazonS3ClientWrapper(IConfiguration configuration)
        {
            this.configuration = configuration;
            accessKeyId = configuration.S3AccessKey;
            accessSecret = configuration.S3SecretKey;
            bucketName = configuration.S3Bucket;
        }

        public string BucketName { 
            get {
                return bucketName;
            } 
        }

        public AmazonS3 CreateInstance()
        {
            return Amazon.AWSClientFactory.CreateAmazonS3Client(accessKeyId, accessSecret);
        }
    }
}