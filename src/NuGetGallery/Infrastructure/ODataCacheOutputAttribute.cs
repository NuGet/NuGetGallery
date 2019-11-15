// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Web.Http.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NuGetGallery.Services;
using WebApi.OutputCache.V2;

namespace NuGetGallery
{
    public class ODataCacheOutputAttribute : CacheOutputAttribute
    {
        private static readonly IReadOnlyDictionary<ODataCachedEndpoint, Func<IODataCacheConfiguration, int>> EndpointToGetSeconds =
            new Dictionary<ODataCachedEndpoint, Func<IODataCacheConfiguration, int>>
            {
                { ODataCachedEndpoint.GetSpecificPackage, x => x.GetSpecificPackageCacheTimeInSeconds },
                { ODataCachedEndpoint.FindPackagesById, x => x.FindPackagesByIdCacheTimeInSeconds },
                { ODataCachedEndpoint.FindPackagesByIdCount, x => x.FindPackagesByIdCountCacheTimeInSeconds },
                { ODataCachedEndpoint.Search, x => x.SearchCacheTimeInSeconds },
            };

        private DynamicSettings _dynamicSettings;
        private readonly object _settingsLock = new object();
        private readonly ODataCachedEndpoint _endpoint;
        private readonly int _defaultServerTimeSpan;

        /// <summary>
        /// The <paramref name="configurationName"/> refers to an integer property name on
        /// <see cref="IODataCacheConfiguration"/>. This property will be uses to override the
        /// <see cref="CacheOutputAttribute.ServerTimeSpan"/>.
        /// </summary>
        public ODataCacheOutputAttribute(ODataCachedEndpoint endpoint, int serverTimeSpan)
        {
            _endpoint = endpoint;
            _defaultServerTimeSpan = serverTimeSpan;
            ServerTimeSpan = serverTimeSpan;
        }

        /// <summary>
        /// This setting determines how frequently the cache duration setting should be checked. Since the OData
        /// endpoints get a lot of traffic, we don't want to check every time. The 60 seconds here is picked to mimic
        /// the feature flag reload time and the value has nothing to do with the cache durations themselves.
        /// </summary>
        public TimeSpan ReloadDuration { get; set; } = TimeSpan.FromSeconds(60);

        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            // This is a very hot path, so cache the configuration and only reload every so often.
            var now = DateTimeOffset.Now;
            var currentSettings = _dynamicSettings;
            if (currentSettings == null || now - currentSettings.CalculatedAt >= ReloadDuration)
            {
                SetLatestSettings(actionContext, now, currentSettings);
            }

            base.OnActionExecuting(actionContext);
        }

        private void SetLatestSettings(HttpActionContext actionContext, DateTimeOffset now, DynamicSettings currentSettings)
        {
            var secondsOrNull = GetSecondsOrNull(actionContext);
            var newServerTimeSpan = secondsOrNull ?? _defaultServerTimeSpan;
            if (newServerTimeSpan != currentSettings?.ServerTimeSpan)
            {
                var logger = GetLogger(actionContext);
                if (secondsOrNull == null)
                {
                    logger.LogInformation(
                        "For OData caching, no dynamic server-side cache time is available for endpoint {Endpoint}. " +
                        "Falling back to the default of {Default} seconds.",
                        _endpoint,
                        _defaultServerTimeSpan);
                }
                else
                {
                    logger.LogInformation(
                        "For OData caching, the server-side cache time of {Seconds} seconds will now be used for endpoint {Endpoint}.",
                        secondsOrNull,
                        _endpoint);
                }

                lock (_settingsLock)
                {
                    if (ReferenceEquals(_dynamicSettings, currentSettings))
                    {
                        ServerTimeSpan = newServerTimeSpan;
                        ResetCacheTimeQuery();
                        _dynamicSettings = new DynamicSettings(now, newServerTimeSpan);
                    }
                }
            }
        }

        private ILogger GetLogger(HttpActionContext actionContext)
        {
            var requestScope = actionContext.Request.GetDependencyScope();
            if (requestScope == null)
            {
                return NullLogger<ODataCacheOutputAttribute>.Instance;
            }

            var logger = requestScope.GetService(typeof(ILogger<ODataCacheOutputAttribute>)) as ILogger<ODataCacheOutputAttribute>;
            if (logger == null)
            {
                return NullLogger<ODataCacheOutputAttribute>.Instance;
            }

            return logger;
        }

        private int? GetSecondsOrNull(HttpActionContext actionContext)
        {
            if (!EndpointToGetSeconds.TryGetValue(_endpoint, out var getSeconds))
            {
                return null;
            }

            var requestScope = actionContext.Request.GetDependencyScope();
            if (requestScope == null)
            {
                return null;
            }

            var featureFlagService = requestScope.GetService(typeof(IFeatureFlagService)) as IFeatureFlagService;
            if (featureFlagService == null || !featureFlagService.AreDynamicODataCacheDurationsEnabled())
            {
                return null;
            }

            var contentObjectService = requestScope.GetService(typeof(IContentObjectService)) as IContentObjectService;
            if (contentObjectService == null)
            {
                return null;
            }

            var config = contentObjectService.ODataCacheConfiguration;
            if (config == null)
            {
                return null;
            }

            return getSeconds(config);
        }

        private class DynamicSettings
        {
            public DynamicSettings(DateTimeOffset calculatedAt, int serverTimeSpan)
            {
                CalculatedAt = calculatedAt;
                ServerTimeSpan = serverTimeSpan;
            }

            public DateTimeOffset CalculatedAt { get; }
            public int ServerTimeSpan { get; }
        }
    }
}