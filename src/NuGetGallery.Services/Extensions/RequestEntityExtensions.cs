// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class RequestEntityExtensions
    {
        private static readonly TimeSpan RequestTokenLifetime = TimeSpan.FromDays(14);

        public static bool IsExpired(this OrganizationMigrationRequest request)
        {
            return IsExpired(request.RequestDate);
        }

        public static bool IsExpired(this MembershipRequest request)
        {
            return IsExpired(request.RequestDate);
        }

        public static bool IsExpired(this PackageOwnerRequest request)
        {
            return IsExpired(request.RequestDate);
        }

        private static bool IsExpired(DateTime requestDate)
        {
            return DateTime.UtcNow - requestDate > RequestTokenLifetime;
        }
    }
}
