// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Mail;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.FeatureFlags
{
    public class FeatureFlagClient : IFeatureFlagClient
    {
        private readonly IFeatureFlagCacheService _cache;
        private readonly ILogger<FeatureFlagClient> _logger;

        public FeatureFlagClient(IFeatureFlagCacheService cache, ILogger<FeatureFlagClient> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsEnabled(string feature, bool defaultValue)
        {
            var latest = _cache.GetLatestFlagsOrNull();
            if (latest == null)
            {
                _logger.LogError(
                    "Couldn't determine status of feature {Feature} as the flags haven't been loaded",
                    feature);

                return defaultValue;
            }

            if (!latest.Features.TryGetValue(feature, out var featureStatus))
            {
                _logger.LogWarning(
                    "Couldn't determine status of feature {Feature} as it isn't in the latest flags",
                    feature);

                return defaultValue;
            }

            switch (featureStatus)
            {
                case FeatureStatus.Enabled:
                    return true;

                case FeatureStatus.Disabled:
                    return false;

                default:
                    _logger.LogError(
                        "Unknown feature status {FeatureStatus} for feature {Feature}",
                        feature,
                        featureStatus);

                    return defaultValue;
            }
        }

        public bool IsEnabled(string flightName, IFlightUser user, bool defaultValue)
        {
            var latest = _cache.GetLatestFlagsOrNull();
            if (latest == null)
            {
                _logger.LogError(
                    "Couldn't determine status of flight {Flight} as the flags haven't been loaded",
                    flightName);

                return defaultValue;
            }

            if (!latest.Flights.TryGetValue(flightName, out var flight))
            {
                _logger.LogWarning(
                    "Couldn't determine status of flight {Flight} as it isn't in the latest feature flags",
                    flightName);

                return defaultValue;
            }

            if (flight.All)
            {
                return true;
            }

            // The user object may be null if the user is anonymous.
            if (user != null)
            {
                if (flight.Accounts.Contains(user.Username, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (TryParseEmailDomain(user.EmailAddress, out var domain) && flight.Domains.Contains(domain, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (flight.SiteAdmins && user.IsSiteAdmin)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryParseEmailDomain(string email, out string domain)
        {
            try
            {
                domain = new MailAddress(email).Host;

                return true;
            }
            catch (Exception) { }

            domain = null;
            return false;
        }
    }
}
