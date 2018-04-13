using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class AwsS3FileSystemStorageService : IFileStorageService
    {
        private readonly IFileSystemService _fileSystemService;
        private readonly Lazy<IAmazonS3> _amazonS3Client;
        private readonly string _amazonS3Bucket;

        public AwsS3FileSystemStorageService(IAppConfiguration configuration, IFileSystemService fileSystemService)
        {
            _fileSystemService = fileSystemService;
            var region = RegionEndpoint.GetBySystemName(configuration.AwsS3Storage_Region);
            _amazonS3Client = new Lazy<IAmazonS3>(() =>
                new AmazonS3Client(
                    new BasicAWSCredentials(configuration.AwsS3Storage_AccessKey, configuration.AwsS3Storage_SecretKey),
                    region));
            _amazonS3Bucket = configuration.AwsS3Storage_Bucket;
        }

        public Task DeleteFileAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = buildPath(folderName, fileName);
            if (_fileSystemService.FileExists(path))
            {
                _fileSystemService.DeleteFile(path);
            }

            return Task.FromResult(0);
        }

        public Task<bool> FileExistsAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = buildPath(folderName, fileName);
            var fileExists = _fileSystemService.FileExists(path);

            return Task.FromResult(fileExists);
        }

        public Task<Stream> GetFileAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = buildPath(folderName, fileName);

            var fileStream = _fileSystemService.FileExists(path) ? _fileSystemService.OpenRead(path) : null;
            return Task.FromResult(fileStream);
        }

        public Task<IFileReference> GetFileReferenceAsync(string folderName, string fileName, string ifNoneMatch = null)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = buildPath(folderName, fileName);
            return Task.FromResult(_fileSystemService.FileExists(path)
                ? _fileSystemService.GetFileReference(path)
                : null);
        }

        public Task<Uri> GetFileReadUriAsync(string folderName, string fileName, DateTimeOffset? endOfAccess)
        {
            if (String.IsNullOrWhiteSpace(folderName))
                throw new ArgumentNullException(nameof(folderName));

            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            if (endOfAccess == null)
                throw new ArgumentNullException(nameof(endOfAccess));

            if (endOfAccess < DateTimeOffset.UtcNow)
                throw new ArgumentOutOfRangeException(nameof(endOfAccess), $"{nameof(endOfAccess)} is in the past");

            var s3Client = _amazonS3Client.Value;

            var preSignedUrl = s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _amazonS3Bucket,
                Expires = endOfAccess.Value.UtcDateTime,
                Key = encodeS3Key(Path.Combine(folderName, fileName)),
                Protocol = Protocol.HTTPS,
                Verb = HttpVerb.GET,
            });

            return Task.FromResult(new Uri(preSignedUrl));
        }

        public Task<Uri> GetPriviledgedFileUriAsync(string folderName, string fileName, FileUriPermissions permissions,
            DateTimeOffset endOfAccess)
        {
            // Amazon S3 does not support multiple file permissions in the same url.
            throw new NotImplementedException();
        }

        public Task SaveFileAsync(string folderName, string fileName, Stream packageFile, bool overwrite = true)
        {
            if (String.IsNullOrWhiteSpace(folderName))
                throw new ArgumentNullException(nameof(folderName));

            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            if (packageFile == null)
                throw new ArgumentNullException(nameof(packageFile));

            var filePath = buildPath(folderName, fileName);
            var dirPath = Path.GetDirectoryName(filePath);

            _fileSystemService.CreateDirectory(dirPath);

            try
            {
                using (var file = _fileSystemService.OpenWrite(filePath, overwrite))
                {
                    packageFile.CopyTo(file);
                }
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    String.Format(
                        CultureInfo.CurrentCulture,
                        "There is already a file with name {0} in folder {1}.",
                        fileName,
                        folderName),
                    ex);
            }

            return Task.FromResult(0);
        }

        public Task CopyFileAsync(Uri srcUri, string destFolderName, string destFileName,
            IAccessCondition destAccessCondition)
        {
            // We could theoretically support this by downloading the source URI to the destination path. This is not
            // needed today so this method will remain unimplemented until it is needed.
            throw new NotImplementedException();
        }

        public Task<string> CopyFileAsync(string srcFolderName, string srcFileName, string destFolderName,
            string destFileName, IAccessCondition destAccessCondition)
        {
            if (srcFolderName == null)
            {
                throw new ArgumentNullException(nameof(srcFolderName));
            }

            if (srcFileName == null)
            {
                throw new ArgumentNullException(nameof(srcFileName));
            }

            if (destFolderName == null)
            {
                throw new ArgumentNullException(nameof(destFolderName));
            }

            if (destFileName == null)
            {
                throw new ArgumentNullException(nameof(destFileName));
            }

            var srcFilePath = buildPath(srcFolderName, srcFileName);
            var destFilePath = buildPath(destFolderName, destFileName);

            _fileSystemService.CreateDirectory(Path.GetDirectoryName(destFilePath));

            if (!_fileSystemService.FileExists(srcFilePath))
                throw new InvalidOperationException("Could not copy because source file does not exists");

            if (_fileSystemService.FileExists(destFilePath))
                throw new InvalidOperationException("Could not copy because destination file already exists");

            try
            {
                _fileSystemService.Copy(srcFilePath, destFilePath, overwrite: false);
            }
            catch (IOException e)
            {
                throw new InvalidOperationException("Could not copy because destination file already exists", e);
            }

            return Task.FromResult<string>(null);
        }

        public Task<ActionResult> CreateDownloadFileActionResultAsync(Uri requestUrl, string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var path = buildPath(folderName, fileName);
            if (!_fileSystemService.FileExists(path))
            {
                return Task.FromResult<ActionResult>(new HttpNotFoundResult());
            }

            var readStream = _fileSystemService.OpenRead(path);
            var result = new FileStreamResult(readStream, "application/octet-stream")
            {
                FileDownloadName = Path.GetFileName(path)
            };
            return Task.FromResult<ActionResult>(result);
        }

        public Task<bool> IsAvailableAsync()
        {
            var client = _amazonS3Client.Value;
            var request = new ListObjectsV2Request
            {
                BucketName = _amazonS3Bucket,
                MaxKeys = 3
            };
            var response = client.ListObjectsV2(request);

            return Task.FromResult(response.HttpStatusCode == HttpStatusCode.OK);
        }

        private static string buildPath(string folderName, string fileName)
        {
            return Path.Combine(folderName, fileName);
        }

        private static string encodeS3Key(string key)
        {
            return key.Replace('\\', '/');
        }
    }
}