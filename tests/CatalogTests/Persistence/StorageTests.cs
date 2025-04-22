using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests.Persistence
{
    public class StorageTests
    {
        [Theory]
        [InlineData("http://contoso.blob.core.windows.net/packages/package.1.0.0.nupkg", "package.1.0.0.nupkg")]
        [InlineData("http://contoso.blob.core.windows.net/packages/another-package123.2.0.0.nupkg", "another-package123.2.0.0.nupkg")]
        public void GetName_NonUnicodeUri_ReturnsCorrectName(string uriString, string expectedName)
        {
            // Arrange
            Uri baseAddress = new Uri("http://contoso.blob.core.windows.net/packages/");
            var storage = new Mock<Storage>(baseAddress) { CallBase = true };
            var uri = new Uri(uriString);

            // Act
            var name = storage.Object.GetUri(expectedName).ToString();

            // Assert
            Assert.EndsWith(expectedName, name);
        }

        [Theory]
        [InlineData("http://contoso.blob.core.windows.net/packages/邮件.1.0.0.nupkg", "邮件.1.0.0.nupkg")]
        [InlineData("http://contoso.blob.core.windows.net/packages/пакет.2.0.0.nupkg", "пакет.2.0.0.nupkg")]
        [InlineData("http://contoso.blob.core.windows.net/packages/パッケージ.3.0.0.nupkg", "パッケージ.3.0.0.nupkg")]
        public void GetName_UnicodeUri_ReturnsCorrectName(string uriString, string expectedName)
        {
            // Arrange
            Uri baseAddress = new Uri("http://contoso.blob.core.windows.net/packages/");
            var storage = new Mock<Storage>(baseAddress) { CallBase = true };
            var uri = new Uri(uriString);

            // Act
            var name = storage.Object.GetUri(expectedName).ToString();

            // Assert
            Assert.EndsWith(expectedName, name);
        }
    }
}
