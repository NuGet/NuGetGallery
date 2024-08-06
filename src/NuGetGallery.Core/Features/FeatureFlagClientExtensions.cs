﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            // The user object is null if the user isn't logged in.
            var flightUser = (user != null)
                ? new FlightUser(user)
                : null;

            return client.IsEnabled(flight, flightUser, defaultValue);
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
