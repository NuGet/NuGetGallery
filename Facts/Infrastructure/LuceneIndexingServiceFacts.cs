﻿using System;
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
        [InlineData("NHibernate", new string[0])]
        [InlineData("NUnit", new string[0])]
        [InlineData("EntityFramework", new[] { "Framework", "Entity" })]
        [InlineData("Sys-netFX", new[] { "Sys", "netFX" })]
        [InlineData("xUnit", new string[0])]
        [InlineData("jQueryUI", new string[0])]
        [InlineData("jQuery-UI", new[] { "jQuery", "UI" })]
        [InlineData("NuGetPowerTools", new[] { "NuGet", "Power", "Tools" } )]
        [InlineData("microsoft-web-helpers", new[] { "microsoft", "web", "helpers" } )]
        [InlineData("EntityFramework.sample", new[] { "EntityFramework", "sample", "Framework", "Entity" })]
        [InlineData("SignalR.MicroSliver", new[] { "SignalR", "MicroSliver", "Micro", "Sliver" })]
        [InlineData("ABCMicroFramework", new[] { "ABC", "Micro", "Framework" })]
        [InlineData("SignalR.Hosting.AspNet", new[] { "SignalR", "Hosting", "AspNet", "Asp", "Net"})] 
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
