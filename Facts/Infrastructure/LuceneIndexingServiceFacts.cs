using System;
using System.Collections.Generic;
using System.Data.Entity;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Infrastructure
{
    public class LuceneIndexingServiceFacts
    {
        [Theory]
        [InlineData(new object[] { "NHibernate", new string[0] })]
        [InlineData(new object[] { "NUnit", new string[0] })]
        [InlineData(new object[] { "SisoDb", new[] { "Siso", "Db" } })]
        [InlineData(new object[] { "EntityFramework", new[] { "Entity", "Framework" } })]
        [InlineData(new object[] { "Sys-netFX", new[] { "Sys", "net", "FX" } })]
        [InlineData(new object[] { "xUnit", new string[0] })]
        [InlineData(new object[] { "jQueryUI", new[] { "jQuery", "UI" } })]
        [InlineData(new object[] { "jQuery-UI", new[] { "jQuery", "UI" } })]
        [InlineData(new object[] { "NuGetPowerTools", new[] { "NuGet", "Power", "Tools" } })]
        [InlineData(new object[] { "microsoft-web-helpers", new[] { "microsoft", "web", "helpers" } })]
        [InlineData(new object[] { "EntityFramework.sample", new[] { "Entity", "Framework", "sample" } })]
        public void CamelCaseTokenizer(string term, IEnumerable<string> tokens)
        {
            // Act
            var result = LuceneIndexingService.TokenizeId(term);

            // Assert
            Assert.Equal(tokens, result);
        }

        [Fact]
        public void UpdateIndexCreatesIndexDirectoryIfNotPresent()
        {
            // Arrange
            var indexingService = new Mock<LuceneIndexingService>() { CallBase = true };
            indexingService.Setup(s => s.CreateContext()).Returns<DbContext>(null);
            indexingService.Setup(s => s.GetPackages(null, null)).Returns(new List<PackageIndexEntity> { new PackageIndexEntity() });
            indexingService.Setup(s => s.GetIndexCreationTime()).Returns<DateTime?>(null);
            indexingService.Setup(s => s.GetLastWriteTime()).Returns<DateTime?>(null);

            indexingService.Setup(s => s.WriteIndex(true, It.IsAny<List<PackageIndexEntity>>())).Verifiable();
            indexingService.Setup(s => s.UpdateLastWriteTime()).Verifiable();
            indexingService.Setup(s => s.EnsureIndexDirectory()).Verifiable();

            // Act
            indexingService.Object.UpdateIndex();

            // Assert
            indexingService.Verify();
        }

        [Fact]
        public void UpdateIndexRecreatesIndexIfOld()
        {
            // Arrange
            var lastWriteTime = DateTime.UtcNow.AddMinutes(-3);
            var indexingService = new Mock<LuceneIndexingService>() { CallBase = true };
            indexingService.Setup(s => s.CreateContext()).Returns<DbContext>(null);
            indexingService.Setup(s => s.GetPackages(null, lastWriteTime)).Returns(new List<PackageIndexEntity>());
            indexingService.Setup(s => s.GetIndexCreationTime()).Returns(DateTime.UtcNow.AddDays(-3).AddMinutes(-1));
            indexingService.Setup(s => s.GetLastWriteTime()).Returns(lastWriteTime);

            indexingService.Setup(s => s.ClearLuceneDirectory()).Verifiable();
            indexingService.Setup(s => s.UpdateLastWriteTime()).Verifiable();

            // Act
            indexingService.Object.UpdateIndex();

            // Assert
            indexingService.Verify();
        }
    }
}
