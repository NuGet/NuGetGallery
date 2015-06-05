// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;

namespace NuGet.Services.Search.Client
{
    public class BaseUrlHealthIndicatorStore : IEndpointHealthIndicatorStore
    {
        private static readonly int[] HealthIndicatorRange = { 100, 90, 75, 50, 25, 20, 15, 10, 5, 1 };

        private readonly IHealthIndicatorLogger _healthIndicatorLogger;
        private readonly ConcurrentDictionary<string, int> _healthIndicators = new ConcurrentDictionary<string, int>();

        public BaseUrlHealthIndicatorStore(IHealthIndicatorLogger healthIndicatorLogger)
        {
            _healthIndicatorLogger = healthIndicatorLogger;
        }

        public int GetHealth(Uri endpoint)
        {
            int health;
            if (!_healthIndicators.TryGetValue(GetBaseUrl(endpoint), out health))
            {
                health = HealthIndicatorRange[0];
            }
            return health;
        }

        public void DecreaseHealth(Uri endpoint, Exception exception)
        {
            var queryLessUri = GetBaseUrl(endpoint);

            _healthIndicators.AddOrUpdate(queryLessUri,

                key =>
                {
                    var health = HealthIndicatorRange[1];
                    _healthIndicatorLogger.LogDecreaseHealth(new Uri(queryLessUri), health, exception);
                    return health;
                }, 

                (key, currentValue) =>
                {
                    var health = HealthIndicatorRange[HealthIndicatorRange.Length - 1];

                    if (currentValue > health)
                    {
                        for (int i = 0; i < HealthIndicatorRange.Length; i++)
                        {
                            if (HealthIndicatorRange[i] < currentValue)
                            {
                                health = HealthIndicatorRange[i];
                                break;
                            }
                        }
                    }

                    if (currentValue != health)
                    {
                        _healthIndicatorLogger.LogDecreaseHealth(new Uri(queryLessUri), health, exception);
                    }
                    return health;
                });
        }

        public void IncreaseHealth(Uri endpoint)
        {
            var queryLessUri = GetBaseUrl(endpoint);

            _healthIndicators.AddOrUpdate(queryLessUri, HealthIndicatorRange[0], (key, currentValue) =>
            {
                var health = HealthIndicatorRange[0];

                if (currentValue < health)
                {
                    for (int i = HealthIndicatorRange.Length - 1; i >= 0; i--)
                    {
                        if (HealthIndicatorRange[i] > currentValue)
                        {
                            health = HealthIndicatorRange[i];
                            break;
                        }
                    }
                }

                if (currentValue != health)
                {
                    _healthIndicatorLogger.LogIncreaseHealth(new Uri(queryLessUri), health);
                }
                return health;
            });
        }

        private static string GetBaseUrl(Uri uri)
        {
            var uriString = uri.ToString();
            var queryStart = uriString.IndexOf("?", StringComparison.Ordinal);
            if (queryStart >= 0)
            {
                return uriString.Substring(0, queryStart);
            }
            return uriString;
        }
    }
}