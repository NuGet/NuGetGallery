using NuGetFeed;
using System;
using System.IO;
using System.Threading;

namespace NuGetFeedTests
{
    public abstract class FunctionalTests : IDisposable
    {
        private readonly string _testRootDirectory;
        private bool _needsCleanup;
        private DirectoryInfo _root;
        private Config _config;

        protected FunctionalTests()
        {
            _needsCleanup = false;
            _testRootDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }

        public DirectoryInfo Root
        {
            get
            {
                if (_root == null)
                {
                    _needsCleanup = true;
                    _root = new DirectoryInfo(_testRootDirectory);
                    _root.Create();
                }

                return _root;
            }
        }

        public Config Config
        {
            get
            {
                if (_config == null)
                {
                    _config = new Config("http://localhost:8000/", Root.FullName);
                }

                return _config;
            }
        }

        public string CreateNupkg(string packageId, string version)
        {
            if (!NupkgInputFolder.Exists)
            {
                NupkgInputFolder.Create();
            }

            return Util.CreateTestPackage(packageId, version, NupkgInputFolder.FullName);
        }

        public DirectoryInfo NupkgInputFolder
        {
            get
            {
                return new DirectoryInfo(Path.Combine(Root.FullName, "nupkgs"));
            }
        }

        public void Dispose()
        {
            if (_needsCleanup)
            {
                _needsCleanup = false;

                while (true)
                try
                {
                    Directory.Delete(_testRootDirectory, true);
                    break;
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
}
