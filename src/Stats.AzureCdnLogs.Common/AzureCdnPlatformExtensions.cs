// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.AzureCdnLogs.Common
{
    public static class AzureCdnPlatformExtensions
    {
        private const string _largeHttpObjectPrefix = "wpc";
        private const string _smallHttpObjectPrefix = "wac";
        private const string _applicationDeliveryNetworkPrefix = "adn";
        private const string _flashMediaStreamingPrefix = "fms";

        public static string GetRawLogFilePrefix(this AzureCdnPlatform platform)
        {
            switch (platform)
            {
                case AzureCdnPlatform.HttpLargeObject:
                    return _largeHttpObjectPrefix;
                case AzureCdnPlatform.HttpSmallObject:
                    return _smallHttpObjectPrefix;
                case AzureCdnPlatform.ApplicationDeliveryNetwork:
                    return _applicationDeliveryNetworkPrefix;
                case AzureCdnPlatform.FlashMediaStreaming:
                    return _flashMediaStreamingPrefix;
                default:
                    throw new ArgumentOutOfRangeException("platform", platform, null);
            }
        }

        public static AzureCdnPlatform ParseAzureCdnPlatformPrefix(string prefix)
        {
            if (string.Equals(prefix, _largeHttpObjectPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                return AzureCdnPlatform.HttpLargeObject;
            }
            if (string.Equals(prefix, _smallHttpObjectPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                return AzureCdnPlatform.HttpSmallObject;
            }
            if (string.Equals(prefix, _applicationDeliveryNetworkPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                return AzureCdnPlatform.ApplicationDeliveryNetwork;
            }
            if (string.Equals(prefix, _flashMediaStreamingPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                return AzureCdnPlatform.FlashMediaStreaming;
            }
            else
            {
                throw new UnknownAzureCdnPlatformException(prefix);
            }
        }
    }
}