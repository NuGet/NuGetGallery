// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ServiceModel;
using NuGet.Services.Search.Client;

namespace NuGetGallery
{
    internal class QuietLogHealthIndicatorLogger
        : IHealthIndicatorLogger
    {
        public void LogDecreaseHealth(Uri endpoint, int health, Exception exception)
        {
            QuietLog.LogHandledException(
                new EndpointNotFoundException(string.Format("ServiceDiscovery - Endpoint '{0}' degraded. Current health: {1}. See inner exception for details.", endpoint, health), exception));
        }

        public void LogIncreaseHealth(Uri endpoint, int health)
        {
            QuietLog.LogHandledException(
                new EndpointNotFoundException(string.Format("ServiceDiscovery - Endpoint '{0}' health increased. Current health: {1}.", endpoint, health)));
        }
    }
}