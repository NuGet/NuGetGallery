// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.FeatureFlags
{
    public interface IFlightUser
    {
        /// <summary>
        /// The user's username.
        /// </summary>
        string Username { get; }

        /// <summary>
        /// The user's email address.
        /// </summary>
        string EmailAddress { get; }

        /// <summary>
        /// Whether the user is an administrator of nuget.org.
        /// </summary>
        bool IsSiteAdmin { get; }
    }
}
