using Moq;
using NuGet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetFeedTests
{
    public class Util
    {

        /// <summary>
        /// Creates a test package. - Take from NuGet.Core.Tests
        /// </summary>
        /// <param name="packageId">The id of the created package.</param>
        /// <param name="version">The version of the created package.</param>
        /// <param name="path">The directory where the package is created.</param>
        /// <returns>The full path of the created package file.</returns>
        public static string CreateTestPackage(string packageId, string version, string path, Uri licenseUrl = null)
        {
            var packageBuilder = new PackageBuilder
            {
                Id = packageId,
                Version = new SemanticVersion(version)
            };
            packageBuilder.Description = string.Format(
                CultureInfo.InvariantCulture,
                "desc of {0} {1}",
                packageId, version);

            if (licenseUrl != null)
            {
                packageBuilder.LicenseUrl = licenseUrl;
            }

            packageBuilder.Files.Add(CreatePackageFile(@"content\test1.txt"));
            packageBuilder.Authors.Add("test author");

            var packageFileName = string.Format("{0}.{1}.nupkg", packageId, version);
            var packageFileFullPath = Path.Combine(path, packageFileName);
            using (var fileStream = File.Create(packageFileFullPath))
            {
                packageBuilder.Save(fileStream);
            }

            return packageFileFullPath;
        }

        /// <summary>
        /// Creates a file with the specified content.
        /// </summary>
        /// <param name="directory">The directory of the created file.</param>
        /// <param name="fileName">The name of the created file.</param>
        /// <param name="fileContent">The content of the created file.</param>
        public static void CreateFile(string directory, string fileName, string fileContent)
        {
            var fileFullName = Path.Combine(directory, fileName);
            using (var writer = new StreamWriter(fileFullName))
            {
                writer.Write(fileContent);
            }
        }

        private static IPackageFile CreatePackageFile(string name)
        {
            var file = new Mock<IPackageFile>();
            file.SetupGet(f => f.Path).Returns(name);
            file.Setup(f => f.GetStream()).Returns(new MemoryStream());

            string effectivePath;
            var fx = VersionUtility.ParseFrameworkNameFromFilePath(name, out effectivePath);
            file.SetupGet(f => f.EffectivePath).Returns(effectivePath);
            file.SetupGet(f => f.TargetFramework).Returns(fx);

            return file.Object;
        }

    }
}
