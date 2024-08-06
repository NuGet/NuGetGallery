// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using Moq;
using NuGetGallery.Services;
using Xunit;

namespace NuGetGallery.Infrastructure
{
    public class HijackSearchServiceFactoryFacts
    {
        [Fact]
        public void ReturnsNonPreviewWhenFeatureDisabled()
        {
            // Arrange
            _featureFlags
                .Setup(f => f.IsPreviewHijackEnabled())
                .Returns(false);

            // Act
            var result = _target.GetService();

            // Assert
            Assert.Equal(_search.Object, result);
            Assert.NotEqual(_previewSearch.Object, result);
        }

        [Fact]
        public void ReturnsNonPreviewAtZeroPercentage()
        {
            // Arrange
            _featureFlags
                .Setup(f => f.IsPreviewHijackEnabled())
                .Returns(true);
            _config
                .Setup(c => c.PreviewHijackPercentage)
                .Returns(0);
            SetupRequest(UserAgentInTestAt50Pct);

            // Act
            var result = _target.GetService();

            // Assert
            Assert.Equal(_search.Object, result);
            Assert.NotEqual(_previewSearch.Object, result);
        }

        [Theory]
        [InlineData(UserAgentInTestAt50Pct)]
        [InlineData(UserAgentNotInTestAt50Pct)]
        public void ReturnsPreviewAt100Percent(string userAgent)
        {
            // Arrange
            _featureFlags
                .Setup(f => f.IsPreviewHijackEnabled())
                .Returns(true);
            _config
                .Setup(c => c.PreviewHijackPercentage)
                .Returns(100);
            SetupRequest(userAgent);

            // Act
            var result = _target.GetService();

            // Assert
            Assert.Equal(_previewSearch.Object, result);
            Assert.NotEqual(_search.Object, result);
        }

        [Theory]
        [InlineData(UserAgentInTestAt50Pct, true)]
        [InlineData(UserAgentNotInTestAt50Pct, false)]
        public void ReturnsPreviewAt50Percent(string userAgent, bool inTest)
        {
            // Arrange
            _featureFlags
                .Setup(f => f.IsPreviewHijackEnabled())
                .Returns(true);
            _config
                .Setup(c => c.PreviewHijackPercentage)
                .Returns(50);
            SetupRequest(userAgent);

            // Act
            var result = _target.GetService();

            // Assert
            if (inTest)
            {
                Assert.Equal(_previewSearch.Object, result);
                Assert.NotEqual(_search.Object, result);
            }
            else
            {
                Assert.Equal(_search.Object, result);
                Assert.NotEqual(_previewSearch.Object, result);
            }
        }

        private void SetupRequest(string userAgent)
        {
            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest
                .Setup(r => r.UserAgent)
                .Returns(userAgent);

            _httpContext
                .Setup(c => c.Request)
                .Returns(httpRequest.Object);
        }

        private const string UserAgentInTestAt50Pct = "Example/1.0.0";
        private const string UserAgentNotInTestAt50Pct = "Example/2.0.0";

        private readonly Mock<HttpContextBase> _httpContext;
        private readonly Mock<IFeatureFlagService> _featureFlags;
        private readonly Mock<IABTestConfiguration> _config;
        private readonly Mock<ISearchService> _search;
        private readonly Mock<ISearchService> _previewSearch;

        private readonly HijackSearchServiceFactory _target;

        public HijackSearchServiceFactoryFacts()
        {
            _httpContext = new Mock<HttpContextBase>();
            _featureFlags = new Mock<IFeatureFlagService>();
            _config = new Mock<IABTestConfiguration>();
            _search = new Mock<ISearchService>();
            _previewSearch = new Mock<ISearchService>();

            var content = new Mock<IContentObjectService>();
            content
                .Setup(c => c.ABTestConfiguration)
                .Returns(_config.Object);

            _target = new HijackSearchServiceFactory(
                _httpContext.Object,
                _featureFlags.Object,
                content.Object,
                _search.Object,
                _previewSearch.Object);
        }
    }
}
