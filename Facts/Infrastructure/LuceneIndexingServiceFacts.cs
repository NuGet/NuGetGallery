using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Infrastructure
{
    public class LuceneIndexingServiceFacts
    {
        [Theory]
        [InlineData("NHibernate", new[] { "NHibernate" })]
        [InlineData("NUnit", new[] { "NUnit" })]
        [InlineData("EntityFramework", new[] { "EntityFramework", "Framework", "Entity" })]
        [InlineData("Sys-netFX", new[] { "Sys-netFX", "Sys", "netFX" })]
        [InlineData("xUnit", new[] { "xUnit" })]
        [InlineData("jQueryUI", new [] { "jQueryUI" })]
        [InlineData("jQuery-UI", new[] { "jQuery-UI", "jQuery", "UI" })]
        [InlineData("NuGetPowerTools", new[] { "NuGetPowerTools", "NuGet", "Power", "Tools" } )]
        [InlineData("microsoft-web-helpers", new[] { "microsoft-web-helpers", "microsoft", "web", "helpers" } )]
        [InlineData("EntityFramework.sample", new[] { "EntityFramework.sample", "EntityFramework", "sample", "Framework", "Entity" })]
        [InlineData("SignalR.MicroSliver", new[] { "SignalR.MicroSliver", "SignalR", "MicroSliver", "Micro", "Sliver" })]
        [InlineData("ABCMicroFramework", new[] { "ABCMicroFramework", "ABC", "Micro", "Framework" })]
        [InlineData("SignalR.Hosting.AspNet", new[] { "SignalR.Hosting.AspNet", "SignalR", "Hosting", "AspNet", "Asp", "Net"})] 
        public void CamelCaseTokenizer(string term, IEnumerable<string> tokens)
        {
            // Act
            var result = LuceneIndexingService.TokenizeId(term);

            // Assert
            Assert.Equal(tokens.OrderBy(p => p), result.OrderBy(p => p));
        }

        [Fact]
        public void UpdateIndexCreatesIndexDirectoryIfNotPresent()
        {
            // Arrange
            var indexingService = new Mock<LuceneIndexingService>() { CallBase = true };
            indexingService.Setup(s => s.CreateContext()).Returns<DbContext>(null);
            indexingService.Setup(s => s.GetPackages(null, null)).Returns(new List<PackageIndexEntity> { new PackageIndexEntity() });
            indexingService.Setup(s => s.GetLastWriteTime()).Returns<DateTime?>(null);

            indexingService.Setup(s => s.WriteIndex(true, It.IsAny<List<PackageIndexEntity>>())).Verifiable();
            indexingService.Setup(s => s.UpdateLastWriteTime()).Verifiable();

            // Act
            indexingService.Object.UpdateIndex();

            // Assert
            indexingService.Verify();
        }
    }
}
