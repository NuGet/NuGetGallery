// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery.TestUtils
{
    public static class ReservedNamespaceServiceTestData
    {
        public static IList<ReservedNamespace> GetTestNamespaces()
        {
            var result = new List<ReservedNamespace>();
            result.Add(new ReservedNamespace("Microsoft.", isSharedNamespace: false, isPrefix: true));
            result.Add(new ReservedNamespace("Microsoft.Aspnet.", isSharedNamespace: false, isPrefix: true));
            result.Add(new ReservedNamespace("baseTest.", isSharedNamespace: false, isPrefix: true));
            result.Add(new ReservedNamespace("jquery", isSharedNamespace: false, isPrefix: false));
            result.Add(new ReservedNamespace("jquery.Extentions.", isSharedNamespace: true, isPrefix: true));
            result.Add(new ReservedNamespace("Random.", isSharedNamespace: false, isPrefix: true));

            return result;
        }

        public static IList<PackageRegistration> GetRegistrations()
        {
            var result = new List<PackageRegistration>();
            result.Add(new PackageRegistration { Id = "Microsoft.Package1", IsVerified = false });
            result.Add(new PackageRegistration { Id = "Microsoft.Package2", IsVerified = false });
            result.Add(new PackageRegistration { Id = "Microsoft.AspNet.Package2", IsVerified = false });
            result.Add(new PackageRegistration { Id = "Random.Package1", IsVerified = false });
            result.Add(new PackageRegistration { Id = "jQuery", IsVerified = false });
            result.Add(new PackageRegistration { Id = "jQuery.Extentions.OwnerView", IsVerified = false });
            result.Add(new PackageRegistration { Id = "jQuery.Extentions.ThirdPartyView", IsVerified = false });
            result.Add(new PackageRegistration { Id = "DeltaX.Test1", IsVerified = false });

            return result;
        }

        public static IList<User> GetTestUsers()
        {
            var key = 0;

            var result = new List<User>();
            result.Add(new User("test1") { Key = key++ });
            result.Add(new User("test2") { Key = key++ });
            result.Add(new User("test3") { Key = key++ });
            result.Add(new User("test4") { Key = key++ });
            result.Add(new User("test5") { Key = key++ });

            return result;
        }
    }
}
