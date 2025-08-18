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
        [InlineData("package.1.0.0.nupkg", "http://contoso.blob.core.windows.net/packages/package.1.0.0.nupkg")]
        [InlineData("123.8.0.0-preview.7.23368.14.nupkg", "http://contoso.blob.core.windows.net/packages/123.8.0.0-preview.7.23368.14.nupkg")]
        public void GetUri_ReturnsCorrectUri(string name, string expectedUri)
        {
            // Arrange
            Uri baseAddress = new Uri("http://contoso.blob.core.windows.net/packages/");
            var storage = new Mock<Storage>(baseAddress) { CallBase = true };

            // Act
            string uri = storage.Object.GetUri(name).AbsoluteUri;

            // Assert
            Assert.Equal(expectedUri, uri);
        }

        [Theory]
        [InlineData("http://contoso.blob.core.windows.net/packages/package.1.0.0.nupkg", "package.1.0.0.nupkg")]
        [InlineData("http://contoso.blob.core.windows.net/packages/123.8.0.0-preview.7.23368.14.nupkg", "123.8.0.0-preview.7.23368.14.nupkg")]
        public void GetName_NonUnicodeUri_ReturnsCorrectName(string uriString, string expectedName)
        {
            // Arrange
            Uri baseAddress = new Uri("http://contoso.blob.core.windows.net/packages/");
            var storage = new Mock<Storage>(baseAddress) { CallBase = true };
            var uri = new Uri(uriString);

            // Act
            var name = storage.Object.GetName(uri).ToString();

            // Assert
            Assert.Equal(expectedName, name);
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
            var name = storage.Object.GetName(uri).ToString();

            // Assert
            Assert.Equal(expectedName, name);
        }

        [Theory]
        [InlineData(
            "https://contoso.blob.core.windows.net/v3-flatcontainer/package123/",
            "https://contoso.blob.core.windows.net/v3-flatcontainer/package123/2.1.0/package123.nuspec",
            "2.1.0/package123.nuspec")]
        [InlineData(
            "https://contoso.blob.core.windows.net/v3-flatcontainer/пакет123/",
            "https://contoso.blob.core.windows.net/v3-flatcontainer/пакет123/2.1.0/пакет123.nuspec",
            "2.1.0/пакет123.nuspec")]
        [InlineData(
            "https://contoso.blob.core.windows.net/v3-flatcontainer/%D0%BF%D0%B0%D0%BA%D0%B5%D1%82123/",
            "https://contoso.blob.core.windows.net/v3-flatcontainer/%D0%BF%D0%B0%D0%BA%D0%B5%D1%82123/2.1.0/%D0%BF%D0%B0%D0%BA%D0%B5%D1%82123.nuspec",
            "2.1.0/пакет123.nuspec")]
        [InlineData(
            "https://contoso.blob.core.windows.net/v3-flatcontainer/пакет123/",
            "https://contoso.blob.core.windows.net/v3-flatcontainer/%D0%BF%D0%B0%D0%BA%D0%B5%D1%82123/2.1.0/%D0%BF%D0%B0%D0%BA%D0%B5%D1%82123.nuspec",
            "2.1.0/пакет123.nuspec")]
        [InlineData(
            "https://contoso.blob.core.windows.net/v3-flatcontainer/%D0%BF%D0%B0%D0%BA%D0%B5%D1%82123/",
            "https://contoso.blob.core.windows.net/v3-flatcontainer/пакет123/2.1.0/%D0%BF%D0%B0%D0%BA%D0%B5%D1%82123.nuspec",
            "2.1.0/пакет123.nuspec")]
        [InlineData(
            "https://contoso.blob.core.windows.net/v3-flatcontainer/%D0%BF%D0%B0%D0%BA%D0%B5%D1%82123/",
            "https://contoso.blob.core.windows.net/v3-flatcontainer/пакет123/2.1.0/пакет123.nuspec",
            "2.1.0/пакет123.nuspec")]
        [InlineData(
            "https://contoso.blob.core.windows.net/v3-flatcontainer/пакет123/",
            "https://contoso.blob.core.windows.net/v3-flatcontainer/%D0%BF%D0%B0%D0%BA%D0%B5%D1%82123/2.1.0/пакет123.nuspec",
            "2.1.0/пакет123.nuspec")]
        public void GetName_EncodedAndDecodedUnicodeUris_ReturnsCorrectName(string baseAddressString, string uriString, string expectedName)
        {
            // Arrange
            Uri baseAddress = new Uri(baseAddressString);
            var storage = new Mock<Storage>(baseAddress) { CallBase = true };
            var uri = new Uri(uriString);

            // Act
            var name = storage.Object.GetName(uri).ToString();

            // Assert
            Assert.Equal(expectedName, name);
        }


        [Theory]
        [InlineData(
            "https://contoso.blob.core.windows.net/v3-flatcontainer/package123/",
            "https://contoso.blob.core.windows.net/v3-flatcontainer/package123/2.1.0/package123.nuspec?sv=2019-12-12&ss=b&srt=co&sp=rlx&se=2023-01-01T00:00:00Z&st=2022-01-01T00:00:00Z&spr=https&sig=abcdef123456",
            "2.1.0/package123.nuspec")]
        [InlineData(
            "https://contoso.blob.core.windows.net/v3-flatcontainer/пакет123/",
            "https://contoso.blob.core.windows.net/v3-flatcontainer/пакет123/2.1.0/пакет123.nuspec?sv=2019-12-12&ss=b&srt=co&sp=rlx&se=2023-01-01T00:00:00Z&st=2022-01-01T00:00:00Z&spr=https&sig=abcdef123456",
            "2.1.0/пакет123.nuspec")]
        public void GetName_UriWithSasToken_RemovesSasTokenAndReturnsCorrectName(string baseAddressString, string uriString, string expectedName)
        {
            // Arrange
            Uri baseAddress = new Uri(baseAddressString);
            var storage = new Mock<Storage>(baseAddress) { CallBase = true };
            var uri = new Uri(uriString);

            // Act
            var name = storage.Object.GetName(uri).ToString();

            // Assert
            Assert.Equal(expectedName, name);
        }

        [Theory]
        [InlineData("http://contoso.blob.core.windows.net/packages/", "https://contoso.blob.core.windows.net/packages/package.1.0.0.nupkg", "package.1.0.0.nupkg")]
        [InlineData("https://contoso.blob.core.windows.net/packages/", "http://contoso.blob.core.windows.net/packages/package.1.0.0.nupkg", "package.1.0.0.nupkg")]
        public void GetName_DifferentSchemes_HandlesSchemeDifferenceCorrectly(string baseAddressString, string uriString, string expectedName)
        {
            // Arrange
            Uri baseAddress = new Uri(baseAddressString);
            var storage = new Mock<Storage>(baseAddress) { CallBase = true };
            var uri = new Uri(uriString);

            // Act
            var name = storage.Object.GetName(uri).ToString();

            // Assert
            Assert.Equal(expectedName, name);
        }

        [Theory]
        [InlineData("https://contoso.blob.core.windows.net/v3-flatcontainer/пакет.2.0.0.nupkg#/metadata/core-properties", "пакет.2.0.0.nupkg")]
        [InlineData("https://contoso.blob.core.windows.net/v3-flatcontainer/пакет.2.0.0.nupkg#/metadata/core-properties/", "пакет.2.0.0.nupkg")]
        [InlineData("https://contoso.blob.core.windows.net/v3-flatcontainer/package123.2.0.0.nupkg#/metadata/core-properties", "package123.2.0.0.nupkg")]
        [InlineData("https://contoso.blob.core.windows.net/v3-flatcontainer/package123.2.0.0.nupkg#/metadata/core-properties/", "package123.2.0.0.nupkg")]
        public void GetName_UriWithFragment_RemovesFragmentAndReturnsCorrectName(string uriString, string expectedName)
        {
            // Arrange
            Uri baseAddress = new Uri("https://contoso.blob.core.windows.net/v3-flatcontainer/");
            var storage = new Mock<Storage>(baseAddress) { CallBase = true };
            var uri = new Uri(uriString);

            // Act
            var name = storage.Object.GetName(uri).ToString();

            // Assert
            Assert.Equal(expectedName, name);
        }
    }
}
