// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Cookies;
using NuGetGallery.Services;
using Xunit;

namespace NuGetGallery
{
    public class CookieBasedABTestServiceFacts
    {
        public class IsPreviewSearchEnabled : BaseSerializationFacts
        {
            [Theory]
            [InlineData(1, 0, false)]
            [InlineData(1, 1, true)]
            [InlineData(49, 50, true)]
            [InlineData(50, 50, true)]
            [InlineData(50, 51, true)]
            [InlineData(99, 100, true)]
            [InlineData(100, 100, true)]
            public async Task ComparesEnrollmentToConfig(int enrollment, int config, bool enabled)
            {
                InitializedEnrollment = new ABTestEnrollment(
                    ABTestEnrollmentState.FirstHit,
                    schemaVersion: 1,
                    previewSearchBucket: enrollment,
                    packageDependentBucket: 42);
                Configuration.Setup(x => x.PreviewSearchPercentage).Returns(config);

                var result = await Target.IsPreviewSearchEnabled(User);

                Assert.Equal(enabled, result);
            }

            protected override async Task<bool> RunTest(User user)
            {
                return await Target.IsPreviewSearchEnabled(user);
            }
        }

        public abstract class BaseSerializationFacts : Facts
        {
            [Fact]
            public async Task ReturnsFalseWhenCookieConsentIsNotGiven()
            {
                CookieComplianceService
                    .Setup(x => x.CanWriteAnalyticsCookies(It.IsAny<HttpRequestBase>()))
                    .Returns(Task.FromResult(false));

                var result = await RunTest(User);

                Assert.False(result, "The test should not be enabled.");
                CookieComplianceService.Verify(x => x.CanWriteAnalyticsCookies(HttpContext.Object.Request), Times.Once);
                Assert.Empty(ResponseCookies);
                EnrollmentFactory.Verify(x => x.Initialize(), Times.Never);
                ABTestEnrollment outEnrollment;
                EnrollmentFactory.Verify(x => x.TryDeserialize(It.IsAny<string>(), out outEnrollment), Times.Never);
            }

            [Fact]
            public async Task ReturnsFalseWhenUserIsNotInFlight()
            {
                FeatureFlagService
                    .Setup(x => x.IsABTestingEnabled(It.IsAny<User>()))
                    .Returns(false);

                var result = await RunTest(User);

                Assert.False(result, "The test should not be enabled.");
                FeatureFlagService.Verify(x => x.IsABTestingEnabled(User), Times.Once);
                Assert.Empty(ResponseCookies);
                EnrollmentFactory.Verify(x => x.Initialize(), Times.Never);
                ABTestEnrollment outEnrollment;
                EnrollmentFactory.Verify(x => x.TryDeserialize(It.IsAny<string>(), out outEnrollment), Times.Never);
            }

            [Fact]
            public async Task InitializesWhenCookieIsMissing()
            {
                var result = await RunTest(User);

                Assert.True(result, "The test should be enabled.");
                Assert.Contains(CookieName, ResponseCookies.Keys.Cast<string>());
                var cookie = ResponseCookies[CookieName];
                VerifyCookie(cookie);
                EnrollmentFactory.Verify(x => x.Initialize(), Times.Once);
                EnrollmentFactory.Verify(x => x.Serialize(InitializedEnrollment), Times.Once);
                EnrollmentFactory.Verify(x => x.Serialize(It.IsAny<ABTestEnrollment>()), Times.Once);
                EnrollmentFactory.Verify(x => x.TryDeserialize(It.IsAny<string>(), out OutEnrollment), Times.Never);
            }

            [Fact]
            public async Task DeserializesWhenCookieIsPresentAndValid()
            {
                RequestCookies.Add(new HttpCookie(CookieName, SerializedEnrollment));

                var result = await RunTest(User);

                Assert.True(result, "The test should be enabled.");
                Assert.Empty(ResponseCookies.Keys.Cast<string>());
                EnrollmentFactory.Verify(x => x.Initialize(), Times.Never);
                EnrollmentFactory.Verify(x => x.Serialize(It.IsAny<ABTestEnrollment>()), Times.Never);
                EnrollmentFactory.Verify(x => x.TryDeserialize(It.IsAny<string>(), out OutEnrollment), Times.Once);
            }

            [Fact]
            public async Task InitializesWhenCookieIsPresentAndInvalid()
            {
                RequestCookies.Add(new HttpCookie(CookieName, SerializedEnrollment));
                EnrollmentFactory
                    .Setup(x => x.TryDeserialize(It.IsAny<string>(), out OutEnrollment))
                    .Returns(false);

                var result = await RunTest(User);

                Assert.True(result, "The test should be enabled.");
                Assert.Contains(CookieName, ResponseCookies.Keys.Cast<string>());
                var cookie = ResponseCookies[CookieName];
                VerifyCookie(cookie);
                EnrollmentFactory.Verify(x => x.Initialize(), Times.Once);
                EnrollmentFactory.Verify(x => x.Serialize(InitializedEnrollment), Times.Once);
                EnrollmentFactory.Verify(x => x.Serialize(It.IsAny<ABTestEnrollment>()), Times.Once);
                EnrollmentFactory.Verify(x => x.TryDeserialize(It.IsAny<string>(), out OutEnrollment), Times.Once);
            }

            /// <summary>
            /// Run the test. The caller expects the method to return true.
            /// </summary>
            protected abstract Task<bool> RunTest(User user);
        }

        public abstract class Facts
        {
            public const string CookieName = "nugetab";

            public Facts()
            {
                HttpContext = new Mock<HttpContextBase>();
                FeatureFlagService = new Mock<IFeatureFlagService>();
                EnrollmentFactory = new Mock<IABTestEnrollmentFactory>();
                ContentObjectService = new Mock<IContentObjectService>();
                Configuration = new Mock<IABTestConfiguration>();
                CookieComplianceService = new Mock<ICookieComplianceService>();
                TelemetryService = new Mock<ITelemetryService>();
                Logger = new Mock<ILogger<CookieBasedABTestService>>();

                User = new User();
                InitializedEnrollment = new ABTestEnrollment(
                    ABTestEnrollmentState.FirstHit,
                    schemaVersion: 1,
                    previewSearchBucket: 23,
                    packageDependentBucket: 47);
                DeserializedEnrollment = new ABTestEnrollment(
                    ABTestEnrollmentState.Active,
                    schemaVersion: 1,
                    previewSearchBucket: 42,
                    packageDependentBucket: 58);
                OutEnrollment = DeserializedEnrollment;
                SerializedEnrollment = "fake-serialization";
                RequestCookies = new HttpCookieCollection();
                ResponseCookies = new HttpCookieCollection();
                PreviewSearchPercentage = 100;

                HttpContext.Setup(x => x.Request.Cookies).Returns(() => RequestCookies);
                HttpContext.Setup(x => x.Response.Cookies).Returns(() => ResponseCookies);
                CookieComplianceService.SetReturnsDefault(Task.FromResult(true));
                FeatureFlagService.SetReturnsDefault(true);

                EnrollmentFactory.Setup(x => x.Initialize()).Returns(() => InitializedEnrollment);
                EnrollmentFactory
                    .Setup(x => x.TryDeserialize(It.IsAny<string>(), out OutEnrollment))
                    .Returns(true);
                EnrollmentFactory
                    .Setup(x => x.Serialize(It.IsAny<ABTestEnrollment>()))
                    .Returns(() => SerializedEnrollment);

                ContentObjectService.Setup(x => x.ABTestConfiguration).Returns(() => Configuration.Object);
                Configuration.Setup(x => x.PreviewSearchPercentage).Returns(() => PreviewSearchPercentage);

                Target = new CookieBasedABTestService(
                    HttpContext.Object,
                    FeatureFlagService.Object,
                    EnrollmentFactory.Object,
                    ContentObjectService.Object,
                    CookieComplianceService.Object,
                    TelemetryService.Object,
                    Logger.Object);
            }

            public Mock<HttpContextBase> HttpContext { get; }
            public Mock<IFeatureFlagService> FeatureFlagService { get; }
            public Mock<IABTestEnrollmentFactory> EnrollmentFactory { get; }
            public Mock<IContentObjectService> ContentObjectService { get; }
            public Mock<IABTestConfiguration> Configuration { get; }
            public Mock<ICookieComplianceService> CookieComplianceService { get; }
            public Mock<ITelemetryService> TelemetryService { get; }
            public Mock<ILogger<CookieBasedABTestService>> Logger { get; }
            public User User { get; }
            public ABTestEnrollment InitializedEnrollment { get; set; }
            public ABTestEnrollment DeserializedEnrollment { get; }
            public ABTestEnrollment OutEnrollment;
            public string SerializedEnrollment { get; }
            public HttpCookieCollection RequestCookies { get; }
            public HttpCookieCollection ResponseCookies { get; }
            public int PreviewSearchPercentage { set; get; }
            public CookieBasedABTestService Target { get; }

            public void VerifyCookie(HttpCookie cookie)
            {
                Assert.NotNull(cookie);
                Assert.Equal(SerializedEnrollment, cookie.Value);
                Assert.True(cookie.HttpOnly, "The cookie should be HTTP only.");
                Assert.True(cookie.Secure, "The cookie should be secure.");
                Assert.Equal(DateTime.MaxValue, cookie.Expires);
                Assert.Equal("/", cookie.Path);
                Assert.Null(cookie.Domain);
                Assert.False(cookie.HasKeys, "The cookie itself should not have keys.");
                Assert.False(cookie.Shareable, "The cookie should not be shareable.");
            }
        }
    }
}
