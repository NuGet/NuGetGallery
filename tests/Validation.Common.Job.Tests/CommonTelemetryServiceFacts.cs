// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Services.Logging;
using Xunit;

namespace Validation.Common.Job.Tests
{
    public class CommonTelemetryServiceFacts
    {
        public class TrackFileDownloaded : BaseFacts
        {
            [Theory]
            [InlineData("/a/b/?foo=bar", "/a/b/")]
            [InlineData("?foo=bar", "/")]
            [InlineData("/?foo=bar", "/")]
            [InlineData("/a?foo=bar&foo=bar", "/a")]
            [InlineData("/a?foo=bar&baz", "/a")]
            [InlineData("/a?foo=bar&", "/a")]
            [InlineData("/a?foo=bar", "/a")]
            [InlineData("/a?foo", "/a")]
            [InlineData("/a?", "/a")]
            [InlineData("/a", "/a")]
            public void StripsQueryString(string inputPath, string expectedPath)
            {
                // Arrange
                var uri = new Uri("http://example" + inputPath);
                var expectedUri = "http://example" + expectedPath;

                // Act
                _target.TrackFileDownloaded(uri, _duration, _size);

                // Assert
                _telemetryClient.Verify(
                    x => x.TrackMetric("FileDownloadedSeconds", _duration.TotalSeconds, It.IsAny<IDictionary<string, string>>()),
                    Times.Once);
                Assert.NotNull(_properties);
                Assert.Equal(new[] { "FileSize", "FileUri" }, _properties.Keys.OrderBy(x => x));
                Assert.Equal("42", _properties["FileSize"]);
                Assert.Equal(expectedUri, _properties["FileUri"]);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly TimeSpan _duration;
            protected readonly int _size;
            protected readonly Mock<ITelemetryClient> _telemetryClient;
            protected readonly CommonTelemetryService _target;
            protected IDictionary<string, string> _properties;

            public BaseFacts()
            {
                _duration = TimeSpan.FromTicks(2018031908);
                _size = 42;
                _properties = null;

                _telemetryClient = new Mock<ITelemetryClient>();

                _telemetryClient
                    .Setup(x => x.TrackMetric(
                        It.IsAny<string>(),
                        It.IsAny<double>(),
                        It.IsAny<IDictionary<string, string>>()))
                    .Callback<string, double, IDictionary<string, string>>((_, __, p) => _properties = p);

                _target = new CommonTelemetryService(_telemetryClient.Object);
            }
        }
    }
}
