using System;
using System.IO;
using System.Security;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGetGallery
{
    public class CloudPackageCacheService : IPackageCacheService
    {
        private readonly string _rootPath;

        public CloudPackageCacheService()
        {
            try
            {
                LocalResource localResource = RoleEnvironment.GetLocalResource("PackageCache");
                _rootPath = localResource.RootPath;
            }
            catch (RoleEnvironmentException exception)
            {
                throw new InvalidOperationException("The local resource isn't defined.", exception);
            }
        }

        public byte[] GetBytes(string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException("'key' cannot be null or empty.", "key");
            }

            string filePath = Path.Combine(_rootPath, key);
            if (!File.Exists(filePath))
            {
                return null;
            }

            return File.ReadAllBytes(filePath);
        }

        public void SetBytes(string key, byte[] item)
        {
            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException("'key' cannot be null or empty.", "key");
            }

            string filePath = Path.Combine(_rootPath, key);

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception)
            {
                return;
            }

            try
            {
                File.WriteAllBytes(filePath, item);
            }
            catch (Exception)
            {
                // One of the possible reasons for this exception is that we exceed the quota of local resource.
                // In that case, delete all files and try again.
                DeleteAllFiles();

                try
                {
                    File.WriteAllBytes(filePath, item);
                }
                catch (Exception)
                {
                    // if the second attempt still fails, move on
                }
            }
        }

        private void DeleteAllFiles()
        {
            foreach (var file in Directory.EnumerateFiles(_rootPath))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
                catch (SecurityException)
                {
                }
            }
        }
    }
}