// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGetGallery.Configuration;

namespace NuGetGallery.Infrastructure.Search
{
    public static class HttpClientBuilderExtensions
    {
        public static IHttpClientBuilder AddSearchPolicyHandlers(
            this IHttpClientBuilder builder,
            ILogger logger,
            string searchName,
            ITelemetryService telemetryService,
            IAppConfiguration config)
        {
            return builder
                .AddPolicyHandler(SearchClientPolicies.SearchClientFallBackCircuitBreakerPolicy(
                    logger,
                    searchName,
                    telemetryService))
                .AddPolicyHandler(SearchClientPolicies.SearchClientWaitAndRetryPolicy(
                    config.SearchCircuitBreakerWaitAndRetryCount,
                    config.SearchCircuitBreakerWaitAndRetryIntervalInMilliseconds,
                    logger,
                    searchName,
                    telemetryService))
                .AddPolicyHandler(SearchClientPolicies.SearchClientCircuitBreakerPolicy(
                    config.SearchCircuitBreakerBreakAfterCount,
                    TimeSpan.FromSeconds(config.SearchCircuitBreakerDelayInSeconds),
                    logger,
                    searchName,
                    telemetryService))
                .AddPolicyHandler(SearchClientPolicies.SearchClientTimeoutPolicy(
                    TimeSpan.FromMilliseconds(config.SearchHttpRequestTimeoutInMilliseconds),
                    logger,
                    searchName,
                    telemetryService));
        }
    }
}