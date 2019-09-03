// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class RequestEntityExtensionsFacts
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);
        private static readonly TimeSpan FuzzFactor = TimeSpan.FromMinutes(1);

        public class IsOrganizationMigrationRequestExpired : IsExpired
        {
            protected override bool Invoke(DateTime requestDate)
            {
                var request = new OrganizationMigrationRequest();
                request.RequestDate = requestDate;

                return request.IsExpired();
            }
        }

        public class IsMembershipRequestExpired : IsExpired
        {
            protected override bool Invoke(DateTime requestDate)
            {
                var request = new MembershipRequest();
                request.RequestDate = requestDate;

                return request.IsExpired();
            }
        }

        public class IsPackageOwnerRequestExpired : IsExpired
        {
            protected override bool Invoke(DateTime requestDate)
            {
                var request = new PackageOwnerRequest();
                request.RequestDate = requestDate;

                return request.IsExpired();
            }
        }

        public abstract class IsExpired
        {
            protected abstract bool Invoke(DateTime requestDate);

            [Fact]
            public void ReturnsFalseForNow()
            {
                var now = DateTime.UtcNow;

                Assert.False(Invoke(now));
            }

            [Fact]
            public void ReturnsFalseForJustBeforeExpiration()
            {
                var justBeforeExpirationTime = DateTime
                    .UtcNow
                    .Subtract(Lifetime)
                    .Add(FuzzFactor);

                Assert.False(Invoke(justBeforeExpirationTime));
            }

            [Fact]
            public void ReturnsTrueForNonExpired()
            {
                var justAfterExpirationTime = DateTime
                    .UtcNow
                    .Subtract(Lifetime)
                    .Subtract(FuzzFactor);

                Assert.True(Invoke(justAfterExpirationTime));
            }
        }
    }
}
