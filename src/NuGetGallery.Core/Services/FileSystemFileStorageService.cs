using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Hosting;

namespace NuGetGallery
{
    public class FileSystemFileStorageService : IFileStorageService
    {
        private readonly string _logicalDirectory;
        private readonly string _storageDirectory;

        public FileSystemFileStorageService(string fileStorageDirectory)
        {
            _logicalDirectory = fileStorageDirectory;
            _storageDirectory = ResolvePath(_logicalDirectory);
        }

        public UriOrStream GetDownloadUriOrStream(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException("folderName");
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            var path = Path.GetFullPath(Path.Combine(_storageDirectory, folderName, fileName));
            if (!File.Exists(path))
            {
                return UriOrStream.NotFound;
            }

            return new UriOrStream(new Uri(path));
        }

        public Task DeleteFileAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException("folderName");
            }
            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            var path = Path.Combine(_storageDirectory, folderName, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.FromResult(0);
        }

        public Task<bool> FileExistsAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException("folderName");
            }
            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            var path = Path.Combine(_storageDirectory, folderName, fileName);
            bool fileExists = File.Exists(path);

            return Task.FromResult(fileExists);
        }

        public Task<Stream> GetFileAsync(string folderName, string fileName)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException("folderName");
            }
            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            var path = Path.Combine(_storageDirectory, folderName, fileName);
            
            Stream fileStream = File.Exists(path) ? File.OpenRead(path) : null;
            return Task.FromResult(fileStream);
        }

        public Task<IFileReference> GetFileReferenceAsync(string folderName, string fileName, string ifNoneMatch = null)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException("folderName");
            }
            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            var path = Path.Combine(_storageDirectory, folderName, fileName);
            
            // Get the last modified date of the file and use that as the ContentID
            var file = new FileInfo(path);
            return Task.FromResult<IFileReference>(file.Exists ? new LocalFileReference(file) : null);
        }

        public Task SaveFileAsync(string folderName, string fileName, Stream packageFile, string contentType)
        {
            if (String.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentNullException("folderName");
            }

            if (String.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            if (packageFile == null)
            {
                throw new ArgumentNullException("packageFile");
            }

            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }

            var folderPath = Path.Combine(_storageDirectory, folderName);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var filePath = Path.Combine(_storageDirectory, folderName, fileName);
            using (var file = File.OpenWrite(filePath))
            {
                packageFile.CopyTo(file);
            }

            return Task.FromResult(0);
        }

        private static string ResolvePath(string fileStorageDirectory)
        {
            if (fileStorageDirectory.StartsWith("~/", StringComparison.OrdinalIgnoreCase) && HostingEnvironment.IsHosted)
            {
                fileStorageDirectory = HostingEnvironment.MapPath(fileStorageDirectory);
            }
            return fileStorageDirectory;
        }

        public Task DownloadToFileAsync(string folderName, string fileName, string downloadedPackageFilePath)
        {
            throw new NotSupportedException("downloading local files to local files sounds crazy. let's not do that unless we need to.");
        }

        public Task UploadFromFileAsync(string folderName, string fileName, string path, string contentType)
        {
            throw new NotSupportedException("uploading local files to local files sounds crazy. let's not do that unless we need to.");
        }
    }
}