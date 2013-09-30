using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class FileSystemFileStorgeServiceScenarios
    {
        private const string FakeConfiguredFileStorageDirectory = "theFileStorageDirectory";

        private static MemoryStream CreateFileStream()
        {
            return new MemoryStream(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0 }, 0, 8, true, true);
        }

        private static FileSystemFileStorageService CreateService()
        {
            string directory = Path.GetRandomFileName().Replace(".", "");
            Assert.False(Directory.Exists(directory));
            return new FileSystemFileStorageService(directory);
        }

        [Fact]
        public void CanSaveAndLoadFilesInANewContainer()
        {
            var service = CreateService();
            string newContainerName = "d" + new Random().Next();

            service.SaveFileAsync(newContainerName, "foo.nupkg", new MemoryStream(new byte[] { 0x42 }), "application/zip").Wait();
            Stream readBack = service.GetFileAsync(newContainerName, "foo.nupkg").Result;

            var x = new MemoryStream(1);
            readBack.CopyTo(x);
            Assert.Equal(new byte[] { 0x42 }, x.GetBuffer());
        }

        public class TheGetDownloadUriOrStreamMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();
                Assert.Throws<ArgumentNullException>(
                    () => service.GetDownloadUriOrStream(folderName, "fileName.xyz"));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();
                Assert.Throws<ArgumentNullException>(
                    () => service.GetDownloadUriOrStream("abcFolder", fileName));
            }

            [Fact]
            public void ReturnsLocalUrlsForFiles_ThatExist()
            {
                var service = CreateService();
                string newContainerName = "d" + new Random().Next();
                service.SaveFileAsync(newContainerName, "nuget.exe", new MemoryStream(new byte[] { 0x42 }), "application/octet-stream").Wait();

                var uriOrStream = service.GetDownloadUriOrStream(newContainerName, "nuget.exe");

                Assert.NotNull(uriOrStream.Uri);
                Assert.True(uriOrStream.Uri.IsFile);
                Assert.NotNull(uriOrStream.Uri.LocalPath);
            }

            [Fact]
            public void ReturnsLocalUrlsOrNullForFiles_ThatDontExistYet()
            {
                var service = CreateService();
                var uriOrStream = service.GetDownloadUriOrStream("downloads", "doesntexist.exe");

                Assert.Null(uriOrStream.Stream);
                if (uriOrStream.Uri != null)
                {
                    Assert.True(uriOrStream.Uri.IsFile);
                    Assert.NotNull(uriOrStream.Uri.LocalPath);
                }
            }
        }

        public class TheDeleteFileMethod
        {
            [Fact]
            public void WillDeleteTheFileIfItExists()
            {
                var service = CreateService();
                string newContainerName = "d" + new Random().Next();
                service.SaveFileAsync(newContainerName, "file.zip", new MemoryStream(new byte[] { 0x42 }), "application/zip").Wait();

                bool existsBefore = service.FileExistsAsync(newContainerName, "file.zip").Result;
                service.DeleteFileAsync(newContainerName, "file.zip").Wait();
                bool existsAfter = service.FileExistsAsync(newContainerName, "file.zip").Result;

                Assert.True(existsBefore);
                Assert.False(existsAfter);
            }

            [Fact]
            public void SucceedsIfTheFileDoesNotExist()
            {
                var service = CreateService();
                string newContainerName = "d" + new Random().Next();

                bool existsBefore = service.FileExistsAsync(newContainerName, "file.noexist").Result;
                service.DeleteFileAsync(newContainerName, "file.noexist").Wait();

                Assert.False(existsBefore);
            }
        }

        public class TheSaveFileMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFolderNameIsNull(string folderName)
            {
                var service = CreateService();

                Assert.Throws<ArgumentNullException>(
                    () => service.SaveFileAsync(folderName, "theFileName", CreateFileStream(), "application/zip"));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfFileNameIsNull(string fileName)
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.SaveFileAsync("theFolderName", fileName, CreateFileStream(), "application/octet-stream"));

                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfFileStreamIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.SaveFileAsync("theFolderName", "theFileName", null, "application/json"));

                Assert.Equal("packageFile", ex.ParamName);
            }
        }   
    }
}