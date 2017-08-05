using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.TestUtils
{
    public static class TestServiceUtility
    {
        private static readonly string _packageHashForTests = "NzMzMS1QNENLNEczSDQ1SA==";

        public static Package CreateTestPackage(string id = null)
        {
            var packageRegistration = new PackageRegistration();
            packageRegistration.Id = string.IsNullOrEmpty(id) ? "test" : id;

            var framework = new PackageFramework();
            var author = new PackageAuthor { Name = "maarten" };
            var dependency = new PackageDependency { Id = "other", VersionSpec = "1.0.0" };

            var package = new Package
            {
                Key = 123,
                PackageRegistration = packageRegistration,
                Version = "1.0.0",
                Hash = _packageHashForTests,
                SupportedFrameworks = new List<PackageFramework>
                {
                    framework
                },
                FlattenedAuthors = "maarten",
                Authors = new List<PackageAuthor>
                {
                    author
                },
                Dependencies = new List<PackageDependency>
                {
                    dependency
                },
                User = new User("test")
            };

            packageRegistration.Packages.Add(package);

            return package;
        }
    }
}
