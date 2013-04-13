using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using Moq;
using Xunit;

namespace NuGetGallery.Services
{
    public class ContentServiceFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void GivenANullFileStorageService_ItShouldThrow()
            {
                ContractAssert.ThrowsArgNull(() => new ContentService(null), "fileStorage");
            }
        }

        public class TheGetContentItemMethod
        {
            const string FileContent = "This is **a** test of http://nuget.org markdown content.";
            const string RenderedFileContent = "<p>This is <strong>a</strong> test of http://nuget.org markdown content.</p>";
            const string CachedContent = "<p>This is <strong>cached</strong> markdown content.</p>";
            const string NewContent = "This is new content!";
            const string RenderedNewContent = "<p>This is new content!</p>";

            [Fact]
            public void GivenANullOrEmptyName_ItShouldThrow()
            {
                ContractAssert.ThrowsArgNullOrEmpty(s => new TestableContentService().GetContentItemAsync(s, TimeSpan.Zero).Wait(), "name");
            }

            [Fact]
            public async Task GivenAContentItemNameAndAnEmptyCache_ItShouldFetchThatItemFromFileStorage()
            {
                // Arrange
                var file = TestFileReference.Create(FileContent);
                var contentService = new TestableContentService();
                contentService.MockFileStorage
                              .Setup(fs => fs.GetFileReferenceAsync(Constants.ContentFolderName, "TestContentItem.md", null))
                              .Returns(Task.FromResult<IFileReference>(file));

                // Act
                var actual = await contentService.GetContentItemAsync("TestContentItem", TimeSpan.Zero);

                // Assert
                Assert.Equal(RenderedFileContent, actual.ToString());
            }

            [Fact]
            public async Task GivenAContentItemNameAndAnEmptyCache_ItShouldPutTheContentInTheCache()
            {
                // Arrange
                var testStart = DateTime.UtcNow;
                var file = TestFileReference.Create(FileContent);
                var contentService = new TestableContentService();
                contentService.MockFileStorage
                              .Setup(fs => fs.GetFileReferenceAsync(Constants.ContentFolderName, "TestContentItem.md", null))
                              .Returns(Task.FromResult<IFileReference>(file));

                // Act
                await contentService.GetContentItemAsync("TestContentItem", TimeSpan.FromSeconds(42));

                // Assert
                var cached = contentService.GetCached("TestContentItem");
                Assert.NotNull(cached);
                Assert.Equal(RenderedFileContent, cached.Content.ToString());
                Assert.Equal(file.ContentId, cached.ContentId);
                Assert.Equal(TimeSpan.FromSeconds(42), cached.ExpiresIn);
                Assert.True(cached.RetrievedUtc >= testStart);
            }

            [Fact]
            public async Task GivenAContentItemNameAndACachedValueThatHasNotExpired_ItShouldFetchThatItemFromCache()
            {
                // Arrange
                var file = TestFileReference.Create(FileContent);
                var contentService = new TestableContentService();
                contentService.SetCached("TestContentItem", CachedContent, TimeSpan.FromDays(365d), DateTime.UtcNow);
                
                // Act
                var actual = await contentService.GetContentItemAsync("TestContentItem", TimeSpan.Zero);

                // Assert
                Assert.Equal(CachedContent, actual.ToString());
                contentService.MockFileStorage
                              .Verify(
                                fs => fs.GetFileReferenceAsync(Constants.ContentFolderName, "TestContentItem.md", It.IsAny<string>()),
                                Times.Never());
            }

            [Fact]
            public async Task GivenAContentItemNameAndACachedValueThatHasExpiredButNotChanged_ItShouldUseTheCachedValue()
            {
                // Arrange
                var testStart = DateTime.UtcNow;
                var file = TestFileReference.Create(CachedContent);
                var contentService = new TestableContentService();
                var cachedContentId = 
                    contentService.SetCached("TestContentItem", CachedContent, TimeSpan.FromSeconds(1), testStart.AddDays(-1d));
                contentService.MockFileStorage
                              .Setup(fs => fs.GetFileReferenceAsync(Constants.ContentFolderName, "TestContentItem.md", cachedContentId))
                              .Returns(Task.FromResult<IFileReference>(file));
                
                // Act
                var actual = await contentService.GetContentItemAsync("TestContentItem", TimeSpan.FromHours(12));

                // Assert
                Assert.Equal(CachedContent, actual.ToString());
                contentService.MockFileStorage
                              .Verify(fs => fs.GetFileReferenceAsync(Constants.ContentFolderName, "TestContentItem.md", cachedContentId));
                Assert.Equal(0, file.OpenCount); // Make sure we never tried to open the file.
                
                var updatedCache = contentService.GetCached("TestContentItem");
                Assert.NotNull(updatedCache);
                Assert.Equal(CachedContent, updatedCache.Content.ToString());
                Assert.Equal(file.ContentId, updatedCache.ContentId);
                Assert.Equal(TimeSpan.FromHours(12), updatedCache.ExpiresIn);
                Assert.True(updatedCache.RetrievedUtc > testStart);
            }

            [Fact]
            public async Task GivenAContentItemAndACachedValueThatHasExpiredAndChanged_ItShouldUseTheFileContent()
            {
                // Arrange
                var testStart = DateTime.UtcNow;
                var file = TestFileReference.Create(NewContent);
                var contentService = new TestableContentService();
                var cachedContentId =
                    contentService.SetCached("TestContentItem", CachedContent, TimeSpan.FromSeconds(1), testStart.AddDays(-1d));
                contentService.MockFileStorage
                              .Setup(fs => fs.GetFileReferenceAsync(Constants.ContentFolderName, "TestContentItem.md", cachedContentId))
                              .Returns(Task.FromResult<IFileReference>(file));

                // Act
                var actual = await contentService.GetContentItemAsync("TestContentItem", TimeSpan.FromHours(12));

                // Assert
                Assert.Equal(RenderedNewContent, actual.ToString());
                contentService.MockFileStorage
                              .Verify(fs => fs.GetFileReferenceAsync(Constants.ContentFolderName, "TestContentItem.md", cachedContentId));
                Assert.Equal(1, file.OpenCount); // Make sure we never tried to open the file.

                var updatedCache = contentService.GetCached("TestContentItem");
                Assert.NotNull(updatedCache);
                Assert.Equal(RenderedNewContent, updatedCache.Content.ToString());
                Assert.Equal(file.ContentId, updatedCache.ContentId);
                Assert.Equal(TimeSpan.FromHours(12), updatedCache.ExpiresIn);
                Assert.True(updatedCache.RetrievedUtc > testStart);
            }
        }

        public class TestableContentService : ContentService
        {
            public Mock<IFileStorageService> MockFileStorage { get; private set; }
            public Mock<ICacheService> MockCache { get; private set; }

            public TestableContentService()
            {
                FileStorage = (MockFileStorage = new Mock<IFileStorageService>()).Object;
            }

            public ContentItem GetCached(string key)
            {
                ContentItem item;
                if (!ContentCache.TryGetValue(key, out item))
                {
                    return null;
                }
                return item;
            }

            public string SetCached(string key, string content, TimeSpan expiresIn, DateTime retrievedUtc)
            {
                var item = new ContentItem(new HtmlString(content), expiresIn, Crypto.Hash(content), retrievedUtc);
                ContentCache.AddOrSet(key, item);
                return item.ContentId;
            }
        }
    }
}