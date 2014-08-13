using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGetFeed
{
    /// <summary>
    /// Factory for creating all paths, storage objects, and emulators for a folder path.
    /// </summary>
    public class ConfigPath
    {
        private readonly string _endpointAddress;
        private readonly string _rootFolder;
        private readonly string _folderName;

        /// <summary>
        /// Create a ConfigPath
        /// </summary>
        /// <param name="endpointAddress">root address that does not include the folder name</param>
        /// <param name="rootFolder">root output folder that does not include the folder name</param>
        /// <param name="folderName">folder name to append</param>
        public ConfigPath(string endpointAddress, string rootFolder, string folderName)
        {
            _endpointAddress = endpointAddress.TrimEnd('/') + "/";

            var dir = new DirectoryInfo(rootFolder);

            _rootFolder = dir.FullName;
            _folderName = folderName.Trim('/').ToLowerInvariant();
        }

        /// <summary>
        /// Base address that includes the folder name.
        /// </summary>
        public Uri BaseAddress
        {
            get
            {
                return new Uri(_endpointAddress + _folderName + "/");
            }
        }

        /// <summary>
        /// The root base address.
        /// </summary>
        public Uri EndpointAddress
        {
            get
            {
                return new Uri(_endpointAddress);
            }
        }

        /// <summary>
        /// Local folder including the folder name
        /// </summary>
        public DirectoryInfo LocalFolder
        {
            get
            {
                DirectoryInfo dir = new DirectoryInfo(Path.Combine(_rootFolder, _folderName));
                return dir;
            }
        }

        public string FolderName
        {
            get
            {
                return _folderName;
            }
        }

        /// <summary>
        /// Root output folder.
        /// </summary>
        public DirectoryInfo RootFolder
        {
            get
            {
                return new DirectoryInfo(_rootFolder);
            }
        }

        public Storage Storage
        {
            get
            {
                return new FileStorage(BaseAddress.AbsoluteUri, LocalFolder.FullName);
            }
        }

        public FileSystemEmulatorHandler FileSystemEmulator
        {
            get
            {
                return new FileSystemEmulatorHandler
                {
                    BaseAddress = new Uri(BaseAddress.AbsoluteUri.TrimEnd('/')),
                    RootFolder = LocalFolder.FullName,
                    InnerHandler = new HttpClientHandler()
                };
            }
        }

    }
}
