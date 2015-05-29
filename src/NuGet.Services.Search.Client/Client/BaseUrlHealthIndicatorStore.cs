// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;

namespace NuGet.Services.Search.Client
{
    public class BaseUrlHealthIndicatorStore : IEndpointHealthIndicatorStore
    {
        private static readonly int[] HealthIndicatorRange = { 100, 90, 75, 50, 25, 20, 15, 10, 5, 1 };
        private readonly ConcurrentDictionary<string, int> _healthIndicators = new ConcurrentDictionary<string, int>();

        public int GetHealth(Uri endpoint)
        {
            int health;
            if (!_healthIndicators.TryGetValue(GetBaseUrl(endpoint), out health))
            {
                health = HealthIndicatorRange[0];
            }
            return health;
        }

        public void DecreaseHealth(Uri endpoint)
        {
            var queryLessUri = GetBaseUrl(endpoint);

            _healthIndicators.AddOrUpdate(queryLessUri, HealthIndicatorRange[1], (key, currentValue) =>
            {
                if (currentValue <= HealthIndicatorRange[HealthIndicatorRange.Length - 1])
                {
                    return HealthIndicatorRange[HealthIndicatorRange.Length - 1];
                }

                for (int i = 0; i < HealthIndicatorRange.Length; i++)
                {
                    if (HealthIndicatorRange[i] < currentValue)
                    {
                        return HealthIndicatorRange[i];
                    }
                }

                return HealthIndicatorRange[HealthIndicatorRange.Length - 1];
            });
        }

        public void IncreaseHealth(Uri endpoint)
        {
            var queryLessUri = GetBaseUrl(endpoint);

            _healthIndicators.AddOrUpdate(queryLessUri, HealthIndicatorRange[0], (key, currentValue) =>
            {
                if (currentValue >= HealthIndicatorRange[0])
                {
                    return HealthIndicatorRange[0];
                }

                for (int i = HealthIndicatorRange.Length - 1; i >= 0; i--)
                {
                    if (HealthIndicatorRange[i] > currentValue)
                    {
                        return HealthIndicatorRange[i];
                    }
                }

                return HealthIndicatorRange[0];
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