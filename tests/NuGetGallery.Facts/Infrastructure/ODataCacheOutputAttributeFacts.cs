// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Web.Http.Routing;
using Autofac;
using Autofac.Integration.WebApi;
using Moq;
using NuGetGallery.Services;
using WebApi.OutputCache.Core.Cache;
using Xunit;

namespace NuGetGallery
{
    public class ODataCacheOutputAttributeFacts
    {
        public class OnActionExecutedAsync : Facts
        {
            [Theory]
            [MemberData(nameof(CacheEndpointTestData))]
            public async Task ObservesConfiguredValue(ODataCachedEndpoint endpoint)
            {
                var cacheTime = 600;
                SetCacheTime(endpoint, cacheTime);
                var target = new ODataCacheOutputAttribute(endpoint, 100);
                target.OnActionExecuting(ActionContext);

                var before = DateTimeOffset.Now;
                await target.OnActionExecutedAsync(ActionExecutedContext, CancellationToken.None);
                var after = DateTimeOffset.Now;

                Cache.Verify(
                    x => x.Add(
                        It.IsAny<string>(),
                        It.IsAny<object>(),
                        It.Is<DateTimeOffset>(e => before.AddSeconds(cacheTime) <= e && e <= after.AddSeconds(cacheTime)),
                        null),
                    Times.Once);
            }

            [Theory]
            [MemberData(nameof(CacheEndpointTestData))]
            public async Task HasCorrectDefaultConfiguredCacheTime(ODataCachedEndpoint endpoint)
            {
                var cacheTime = DefaultCacheTime[endpoint];
                var target = new ODataCacheOutputAttribute(endpoint, 0);
                target.OnActionExecuting(ActionContext);

                var before = DateTimeOffset.Now;
                await target.OnActionExecutedAsync(ActionExecutedContext, CancellationToken.None);
                var after = DateTimeOffset.Now;

                if (cacheTime <= 0)
                {
                    Cache.Verify(
                        x => x.Add(
                            It.IsAny<string>(),
                            It.IsAny<object>(),
                            It.IsAny<DateTimeOffset>(),
                            It.IsAny<string>()),
                        Times.Never);
                }
                else
                {
                    Cache.Verify(
                        x => x.Add(
                            It.IsAny<string>(),
                            It.IsAny<object>(),
                            It.Is<DateTimeOffset>(e => before.AddSeconds(cacheTime) <= e && e <= after.AddSeconds(cacheTime)),
                            null),
                        Times.Once);
                }
            }

            [Fact]
            public async Task UsesDefaultCacheValueWhenFeatureFlagIsOff()
            {
                FeatureFlagService.Setup(x => x.AreDynamicODataCacheDurationsEnabled()).Returns(false);
                SetCacheTime(ODataCachedEndpoint.GetSpecificPackage, 600);
                var target = new ODataCacheOutputAttribute(ODataCachedEndpoint.GetSpecificPackage, 100);
                target.OnActionExecuting(ActionContext);

                var before = DateTimeOffset.Now;
                await target.OnActionExecutedAsync(ActionExecutedContext, CancellationToken.None);
                var after = DateTimeOffset.Now;

                Cache.Verify(
                    x => x.Add(
                        It.IsAny<string>(),
                        It.IsAny<object>(),
                        It.Is<DateTimeOffset>(e => before.AddSeconds(100) <= e && e <= after.AddSeconds(100)),
                        null),
                    Times.Once);
            }

            [Fact]
            public async Task RevertsToDefaultValueWhenFeatureFlagIsTurnedOff()
            {
                SetCacheTime(ODataCachedEndpoint.GetSpecificPackage, 600);
                var target = new ODataCacheOutputAttribute(ODataCachedEndpoint.GetSpecificPackage, 100)
                {
                    ReloadDuration = TimeSpan.Zero,
                };
                target.OnActionExecuting(ActionContext);
                await target.OnActionExecutedAsync(ActionExecutedContext, CancellationToken.None);
                FeatureFlagService.Setup(x => x.AreDynamicODataCacheDurationsEnabled()).Returns(false);
                Cache.ResetCalls();
                target.OnActionExecuting(ActionContext);

                var before = DateTimeOffset.Now;
                await target.OnActionExecutedAsync(ActionExecutedContext, CancellationToken.None);
                var after = DateTimeOffset.Now;

                Cache.Verify(
                    x => x.Add(
                        It.IsAny<string>(),
                        It.IsAny<object>(),
                        It.Is<DateTimeOffset>(e => before.AddSeconds(100) <= e && e <= after.AddSeconds(100)),
                        null),
                    Times.Once);
            }

            [Fact]
            public async Task CanChangeCacheTime()
            {
                SetCacheTime(ODataCachedEndpoint.GetSpecificPackage, 300);
                var target = new ODataCacheOutputAttribute(ODataCachedEndpoint.GetSpecificPackage, 100)
                {
                    ReloadDuration = TimeSpan.Zero,
                };
                target.OnActionExecuting(ActionContext);
                await target.OnActionExecutedAsync(ActionExecutedContext, CancellationToken.None);
                SetCacheTime(ODataCachedEndpoint.GetSpecificPackage, 600);
                Cache.ResetCalls();
                target.OnActionExecuting(ActionContext);

                var before = DateTimeOffset.Now;
                await target.OnActionExecutedAsync(ActionExecutedContext, CancellationToken.None);
                var after = DateTimeOffset.Now;

                Cache.Verify(
                    x => x.Add(
                        It.IsAny<string>(),
                        It.IsAny<object>(),
                        It.Is<DateTimeOffset>(e => before.AddSeconds(600) <= e && e <= after.AddSeconds(600)),
                        null),
                    Times.Once);
            }

            [Fact]
            public async Task CanUseZeroWhenConfigurationIsTurnedOff()
            {
                SetCacheTime(ODataCachedEndpoint.GetSpecificPackage, 600);
                var target = new ODataCacheOutputAttribute(ODataCachedEndpoint.GetSpecificPackage, 0);
                FeatureFlagService.Setup(x => x.AreDynamicODataCacheDurationsEnabled()).Returns(false);
                target.OnActionExecuting(ActionContext);

                await target.OnActionExecutedAsync(ActionExecutedContext, CancellationToken.None);

                Cache.Verify(
                    x => x.Add(
                        It.IsAny<string>(),
                        It.IsAny<object>(),
                        It.IsAny<DateTimeOffset>(),
                        It.IsAny<string>()),
                    Times.Never);
            }

            [Fact]
            public async Task CanUseZeroFromConfiguration()
            {
                SetCacheTime(ODataCachedEndpoint.GetSpecificPackage, 0);
                var target = new ODataCacheOutputAttribute(ODataCachedEndpoint.GetSpecificPackage, 100);
                target.OnActionExecuting(ActionContext);

                await target.OnActionExecutedAsync(ActionExecutedContext, CancellationToken.None);

                Cache.Verify(
                    x => x.Add(
                        It.IsAny<string>(),
                        It.IsAny<object>(),
                        It.IsAny<DateTimeOffset>(),
                        It.IsAny<string>()),
                    Times.Never);
            }

            [Fact]
            public async Task CachesSettings()
            {
                SetCacheTime(ODataCachedEndpoint.GetSpecificPackage, 300);
                var target = new ODataCacheOutputAttribute(ODataCachedEndpoint.GetSpecificPackage, 100)
                {
                    ReloadDuration = TimeSpan.FromHours(1),
                };
                target.OnActionExecuting(ActionContext);
                await target.OnActionExecutedAsync(ActionExecutedContext, CancellationToken.None);
                SetCacheTime(ODataCachedEndpoint.GetSpecificPackage, 600);
                Cache.ResetCalls();
                target.OnActionExecuting(ActionContext);

                var before = DateTimeOffset.Now;
                await target.OnActionExecutedAsync(ActionExecutedContext, CancellationToken.None);
                var after = DateTimeOffset.Now;

                Cache.Verify(
                    x => x.Add(
                        It.IsAny<string>(),
                        It.IsAny<object>(),
                        It.Is<DateTimeOffset>(e => before.AddSeconds(300) <= e && e <= after.AddSeconds(300)),
                        null),
                    Times.Once);
            }
        }

        /// <summary>
        /// Helpers from:
        /// https://github.com/OData/WebApi/blob/5061dcad757599b93ff990079832123a73e0e091/test/UnitTest/Microsoft.AspNet.OData.Test.Shared/ContextUtil.cs
        /// https://stackoverflow.com/questions/11848137/how-can-you-unit-test-an-action-filter-in-asp-net-web-api
        /// </summary>
        public abstract class Facts
        {
            public Facts()
            {
                ContentObjectService = new Mock<IContentObjectService>();
                FeatureFlagService = new Mock<IFeatureFlagService>();
                Cache = new Mock<IApiOutputCache>();

                CacheConfiguration = new ODataCacheConfiguration();
                ContentObjectService.Setup(x => x.ODataCacheConfiguration).Returns(() => CacheConfiguration);
                FeatureFlagService.SetReturnsDefault(true);
                FeatureFlagService.Setup(x => x.AreDynamicODataCacheDurationsEnabled()).Returns(() => true);

                var builder = new ContainerBuilder();
                builder.Register(c => ContentObjectService.Object);
                builder.Register(c => FeatureFlagService.Object);
                builder.Register(c => Cache.Object);
                HttpConfiguration = new HttpConfiguration();
                HttpConfiguration.DependencyResolver = new AutofacWebApiDependencyResolver(builder.Build());

                ActionDescriptor = new Mock<HttpActionDescriptor> { CallBase = true };
                ActionDescriptor.Setup(x => x.ActionName).Returns("FooAction");
                ActionDescriptor.Setup(x => x.ReturnType).Returns(typeof(HttpResponseMessage));
                ControllerContext = CreateControllerContext(HttpConfiguration);
                ActionContext = CreateActionContext(ControllerContext, ActionDescriptor.Object);
                ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("Got 'em.", Encoding.UTF8, "text/plain"),
                };
                ActionExecutedContext = CreateActionExecutedContext(ActionContext, ResponseMessage);
            }

            public static HttpControllerContext CreateControllerContext(
                HttpConfiguration configuration = null,
                IHttpController instance = null,
                IHttpRouteData routeData = null,
                HttpRequestMessage request = null)
            {
                HttpConfiguration config = configuration ?? new HttpConfiguration();
                IHttpRouteData route = routeData ?? new HttpRouteData(new HttpRoute());
                HttpRequestMessage req = request ?? new HttpRequestMessage();
                req.SetConfiguration(config);
                req.SetRouteData(route);

                HttpControllerContext context = new HttpControllerContext(config, route, req);
                if (instance != null)
                {
                    context.Controller = instance;
                }
                context.ControllerDescriptor = CreateControllerDescriptor(config);

                return context;
            }

            public static HttpControllerDescriptor CreateControllerDescriptor(HttpConfiguration config = null)
            {
                if (config == null)
                {
                    config = new HttpConfiguration();
                }

                return new HttpControllerDescriptor
                {
                    Configuration = config,
                    ControllerName = "FooController",
                    ControllerType = typeof(ApiController),
                };
            }

            public static HttpActionContext CreateActionContext(HttpControllerContext controllerContext = null, HttpActionDescriptor actionDescriptor = null)
            {
                HttpControllerContext context = controllerContext ?? CreateControllerContext();
                HttpActionDescriptor descriptor = actionDescriptor ?? new Mock<HttpActionDescriptor>() { CallBase = true }.Object;
                return new HttpActionContext(context, descriptor);
            }

            public static HttpActionExecutedContext CreateActionExecutedContext(
                HttpActionContext actionContext,
                HttpResponseMessage response)
            {
                return new HttpActionExecutedContext(actionContext, null) { Response = response };
            }

            public static IEnumerable<object[]> CacheEndpointTestData => Enum
                .GetValues(typeof(ODataCachedEndpoint))
                .Cast<ODataCachedEndpoint>()
                .Select(x => new object[] { x });

            public static readonly IReadOnlyDictionary<ODataCachedEndpoint, int> DefaultCacheTime =
                new Dictionary<ODataCachedEndpoint, int>()
                {
                    { ODataCachedEndpoint.GetSpecificPackage, 60 },
                    { ODataCachedEndpoint.FindPackagesById, 60 },
                    { ODataCachedEndpoint.FindPackagesByIdCount, 0 },
                    { ODataCachedEndpoint.Search, 45 },
                };

            public void SetCacheTime(ODataCachedEndpoint endpoint, int cacheTime)
            {
                switch (endpoint)
                {
                    case ODataCachedEndpoint.GetSpecificPackage:
                        CacheConfiguration.GetSpecificPackageCacheTimeInSeconds = cacheTime;
                        break;
                    case ODataCachedEndpoint.FindPackagesById:
                        CacheConfiguration.FindPackagesByIdCacheTimeInSeconds = cacheTime;
                        break;
                    case ODataCachedEndpoint.FindPackagesByIdCount:
                        CacheConfiguration.FindPackagesByIdCountCacheTimeInSeconds = cacheTime;
                        break;
                    case ODataCachedEndpoint.Search:
                        CacheConfiguration.SearchCacheTimeInSeconds = cacheTime;
                        break;
                }
            }

            public Mock<IContentObjectService> ContentObjectService { get; }
            public Mock<IFeatureFlagService> FeatureFlagService { get; }
            public Mock<IApiOutputCache> Cache { get; }
            public Mock<HttpActionDescriptor> ActionDescriptor { get; }
            public ODataCacheConfiguration CacheConfiguration { get; }
            public HttpConfiguration HttpConfiguration { get; }
            public HttpControllerContext ControllerContext { get; }
            public HttpActionContext ActionContext { get; }
            public HttpResponseMessage ResponseMessage { get; }
            public HttpActionExecutedContext ActionExecutedContext { get; }
        }
    }
}
