﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class CloudDownloadCountServiceFacts
    {
        public class TheTryGetDownloadCountForPackageRegistrationMethod : BaseFacts
        {
            [Theory]
            [InlineData("NuGet.Versioning", "NuGet.Frameworks")]
            [InlineData("NuGet.Versioning", " NuGet.Versioning ")]
            [InlineData("NuGet.Versioning", "NuGet.Versioning ")]
            [InlineData("NuGet.Versioning", " NuGet.Versioning")]
            [InlineData("İ", "ı")]
            [InlineData("İ", "i")]
            [InlineData("İ", "I")]
            [InlineData("ı", "İ")]
            [InlineData("ı", "i")]
            [InlineData("ı", "I")]
            [InlineData("i", "İ")]
            [InlineData("i", "ı")]
            [InlineData("I", "İ")]
            [InlineData("I", "ı")]
            public void ReturnsZeroWhenIdDoesNotExist(string inputId, string contentId)
            {
                // Arrange
                _content = $"[[\"{contentId}\",[\"4.6.0\",23],[\"4.6.2\",42]]";
                _target.Refresh();

                // Act
                var found = _target.TryGetDownloadCountForPackageRegistration(inputId, out var actual);

                // Assert
                Assert.Equal(0, actual);
                Assert.False(found, "The package ID should not have been found.");
            }

            [Theory]
            [InlineData("NuGet.Versioning", "NuGet.Versioning")]
            [InlineData("NUGET.VERSIONING", "nuget.versioning")]
            [InlineData("nuget.versioning", "NUGET.VERSIONING")]
            [InlineData("İ", "İ")]
            [InlineData("ı", "ı")]
            public void ReturnsSumOfVersionsWhenIdExists(string inputId, string contentId)
            {
                // Arrange
                _content = $"[[\"{contentId}\",[\"4.6.0\",23],[\"4.6.2\",42]]";
                _target.Refresh();

                // Act
                var found = _target.TryGetDownloadCountForPackageRegistration(inputId, out var actual);

                // Assert
                Assert.Equal(23 + 42, actual);
                Assert.True(found, "The package ID should have been found.");
            }

            [Fact]
            public async Task CanCalculatedDownloadCountsDuringRefresh()
            {
                // Arrange
                var id = "NuGet.Versioning";
                var duration = TimeSpan.FromSeconds(3);
                var refreshTask = LoadNewVersionsAsync(id, TimeSpan.FromSeconds(1));
                var getDownloadCountsTask = GetDownloadCountsAsync(id, duration);
                _calculateSum = v => v.Sum(kvp => { Thread.Sleep(1); return kvp.Value; });

                // Act & Assert
                // We are just ensuring that no exception is thrown.
                await Task.WhenAll(refreshTask, getDownloadCountsTask);
            }

            private async Task LoadNewVersionsAsync(string id, TimeSpan duration)
            {
                await Task.Yield();
                var stopwatch = Stopwatch.StartNew();
                var iteration = 0;
                while (stopwatch.Elapsed < duration)
                {
                    iteration++;
                    var version = $"0.0.0-beta{iteration}";
                    _content = $"[[\"{id}\",[\"{version}\",1]]";
                    _target.Refresh();
                    await Task.Delay(5);
                }
            }

            private async Task GetDownloadCountsAsync(string id, TimeSpan duration)
            {
                await Task.Yield();
                var stopwatch = Stopwatch.StartNew();
                while (stopwatch.Elapsed < duration)
                {
                    _target.TryGetDownloadCountForPackageRegistration(id, out var downloadCount);
                    await Task.Delay(5);
                }
            }
        }

        public class TheTryGetDownloadCountForPackageMethod : BaseFacts
        {
            [Fact]
            public void ReturnsZeroWhenVersionDoesNotExist()
            {
                // Arrange
                _target.Refresh();

                // Act
                var found = _target.TryGetDownloadCountForPackage("NuGet.Versioning", "9.9.9", out var actual);

                // Assert
                Assert.Equal(0, actual);
                Assert.False(found, "The package version should not have been found.");
            }

            [Fact]
            public void ReturnsCountWhenVersionExists()
            {
                // Arrange
                _target.Refresh();

                // Act
                var found = _target.TryGetDownloadCountForPackage("NuGet.Versioning", "4.6.0", out var actual);

                // Assert
                Assert.Equal(23, actual);
                Assert.True(found, "The package version should have been found.");
            }
        }

        public class BaseFacts
        {
            internal readonly Mock<ITelemetryClient> _telemetryService;
            internal string _content;
            internal Func<IDictionary<string, int>, int> _calculateSum;
            internal TestableCloudDownloadCountService _target;

            public BaseFacts()
            {
                _telemetryService = new Mock<ITelemetryClient>();
                _content = "[[\"NuGet.Versioning\",[\"4.6.0\",23],[\"4.6.2\",42]]";
                _calculateSum = null;
                _target = new TestableCloudDownloadCountService(this);
            }
        }

        public class TestableCloudDownloadCountService : CloudDownloadCountService
        {
            private readonly BaseFacts _baseFacts;

            public TestableCloudDownloadCountService(BaseFacts baseFacts)
                    : base(baseFacts._telemetryService.Object, "UseDevelopmentStorage=true", readAccessGeoRedundant: true)
            {
                _baseFacts = baseFacts;
            }

            protected override int CalculateSum(ConcurrentDictionary<string, int> versions)
            {
                if (_baseFacts._calculateSum == null)
                {
                    return base.CalculateSum(versions);
                }

                return _baseFacts._calculateSum(versions);
            }

            protected override Stream GetBlobStream()
            {
                if (_baseFacts._content == null)
                {
                    return null;
                }

                return new MemoryStream(Encoding.UTF8.GetBytes(_baseFacts._content));
            }
        }
    }
}
