using System;
using System.IO;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.IO;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class AwsS3FileSystemService : IFileSystemService
    {
        private readonly IAmazonS3 _amazonS3Client;
        private readonly string _amazonS3Bucket;
        private readonly string _rootDirectory;

        public AwsS3FileSystemService(IAppConfiguration configuration)
        {
            var region = RegionEndpoint.GetBySystemName(configuration.AwsS3Storage_Region);
            _amazonS3Client =new AmazonS3Client(new BasicAWSCredentials(configuration.AwsS3Storage_AccessKey, configuration.AwsS3Storage_SecretKey),region);
            _amazonS3Bucket = configuration.AwsS3Storage_Bucket;
            _rootDirectory = configuration.AwsS3Storage_RootDirectory ?? string.Empty;
        }

        public void CreateDirectory(string path)
        {
            new S3DirectoryInfo(_amazonS3Client, _amazonS3Bucket, buildPath(path)).Create();
        }

        public void DeleteFile(string path)
        {
            new S3FileInfo(_amazonS3Client, _amazonS3Bucket, buildPath(path)).Delete();
        }

        public bool DirectoryExists(string path)
        {
            return new S3DirectoryInfo(_amazonS3Client, _amazonS3Bucket, buildPath(path)).Exists;
        }

        public bool FileExists(string path)
        {
            return new S3FileInfo(_amazonS3Client, _amazonS3Bucket, buildPath(path)).Exists;
        }

        public Stream OpenRead(string path)
        {
            return new S3FileInfo(_amazonS3Client, _amazonS3Bucket, buildPath(path)).OpenRead();
        }

        public Stream OpenWrite(string path, bool overwrite)
        {
            if (!overwrite && FileExists(buildPath(path)))
                throw new IOException("File already exists");

            return new S3FileInfo(_amazonS3Client, _amazonS3Bucket, buildPath(path)).OpenWrite();
        }

        ///AWS S3 does not support recording of the creation time.
        public DateTimeOffset GetCreationTimeUtc(string path)
        {
            throw new NotImplementedException();
        }

        public IFileReference GetFileReference(string path)
        {
            var info = new S3FileInfo(_amazonS3Client, _amazonS3Bucket, buildPath(path));
            return info.Exists ? new AwsS3FileReference(info) : null;
        }

        public void Copy(string sourceFileName, string destFileName, bool overwrite)
        {
            using(var sourceFile = OpenRead(sourceFileName))
            using (var destinationFile = OpenWrite(destFileName, overwrite))
            {
                sourceFile.CopyTo(destinationFile);
                destinationFile.Flush();
            }
        }

        private string buildPath(string path)
        {
            return Path.Combine(_rootDirectory, path);
        }
    }
}