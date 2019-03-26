// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGet.Services.FeatureFlags;

namespace NuGetGallery.Features
{
    public static class FeatureFlagClientExtensions
    {
        public static bool IsEnabled(
            this IFeatureFlagClient client,
            string flight,
            User user,
            bool defaultValue)
        {
            if (user == null)
            {
                // The current user is null when not logged in.
                // Return the default value if the user is not logged in.
                return defaultValue;
            }

            return client.IsEnabled(flight, new FlightUser(user), defaultValue);
        }

        private class FlightUser : IFlightUser
        {
            public FlightUser(User user)
            {
                Username = user.Username;
                EmailAddress = user.EmailAddress;
                IsSiteAdmin = user.IsAdministrator;
            }

            public string Username { get; }
            public string EmailAddress { get; }
            public bool IsSiteAdmin { get; }
        }
    }
}
