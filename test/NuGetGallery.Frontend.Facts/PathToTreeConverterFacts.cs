using System;
using System.Linq;
using Moq;
using NuGet;
using NuGetGallery.ViewModels.PackagePart;
using Xunit;

namespace NuGetGallery
{
    public class PathToTreeConverterFacts
    {
        [Fact]
        public void CtorThrowsIfArgumentIsNull()
        {
            Assert.Throws(typeof(ArgumentNullException), () => { PathToTreeConverter.Convert(null); });
        }

        [Fact]
        public void ConvertWithOneFile()
        {
            // Arrange
            var files = new IPackageFile[] {
                CreatePackageFile(@"one.txt")
            };

            // Act
            var root = PathToTreeConverter.Convert(files);

            // Assert
            Assert.NotNull(root);
            AssertItem(root, "", 1);
            AssertItem(root.Children.ElementAt(0), "one.txt", 0);
        }

        [Fact]
        public void ConvertWithTwoFilesInTheSameFolder()
        {
            // Arrange
            var files = new IPackageFile[] {
                CreatePackageFile(@"content\one.txt"),
                CreatePackageFile(@"content\two.txt")
            };

            // Act
            var root = PathToTreeConverter.Convert(files);

            // Assert
            Assert.NotNull(root);
            AssertItem(root, "", 1);

            var contentNode = root.Children.ElementAt(0);
            AssertItem(contentNode, "content", 2);

            var firstChild = contentNode.Children.ElementAt(0);
            AssertItem(firstChild, "one.txt", 0);

            var secondChild = contentNode.Children.ElementAt(1);
            AssertItem(secondChild, "two.txt", 0);
        }

        [Fact]
        public void ConvertWithTwoFilesInDifferentFolders()
        {
            // Arrange
            var files = new IPackageFile[] {
                CreatePackageFile(@"content\one.txt"),
                CreatePackageFile(@"lib\two.dll")
            };

            // Act
            var root = PathToTreeConverter.Convert(files);

            // Assert
            Assert.NotNull(root);
            AssertItem(root, "", 2);

            var contentNode = root.Children.ElementAt(0);
            AssertItem(contentNode, "content", 1);

            var libNode = root.Children.ElementAt(1);
            AssertItem(libNode, "lib", 1);

            var firstChild = contentNode.Children.ElementAt(0);
            AssertItem(firstChild, "one.txt", 0);

            var secondChild = libNode.Children.ElementAt(0);
            AssertItem(secondChild, "two.dll", 0);
        }

        [Fact]
        public void ConvertWithThreeFilesInDifferentFolders()
        {
            // Arrange
            var files = new IPackageFile[] {
                CreatePackageFile(@"jQuery.js"),
                CreatePackageFile(@"lib\two.dll"),
                CreatePackageFile(@"readme.txt"),
            };

            // Act
            var root = PathToTreeConverter.Convert(files);

            // Assert
            Assert.NotNull(root);
            AssertItem(root, "", 3);

            var libNode = root.Children.ElementAt(0);
            AssertItem(libNode, "lib", 1);

            var jQuery = root.Children.ElementAt(1);
            AssertItem(jQuery, "jQuery.js", 0);

            var readme = root.Children.ElementAt(2);
            AssertItem(readme, "readme.txt", 0);

            var secondChild = libNode.Children.ElementAt(0);
            AssertItem(secondChild, "two.dll", 0);
        }

        private static void AssertItem(PackageItem item, string name, int numberOfChildren) 
        {
            Assert.Equal(name, item.Name);
            Assert.Equal(numberOfChildren, item.Children.Count);
        }

        private static IPackageFile CreatePackageFile(string path)
        {
            var file = new Mock<IPackageFile>();
            file.Setup(s => s.Path).Returns(path);
            return file.Object;
        }
    }
}
