// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Entity;
using Moq;
using NuGet.Services.Entities;

namespace Tests.ContextHelpers
{
    using GalleryContext = NuGetGallery.IEntitiesContext;

    public static class ContextExtensions
    {
        public static void Mock(
            this Mock<GalleryContext> context,
            Mock<DbSet<PackageRegistration>> packageRegistrationsMock = null,
            Mock<DbSet<PackageDependency>> packageDependenciesMock = null,
            Mock<DbSet<Package>> packagesMock = null,
            Mock<DbSet<Certificate>> certificatesMock = null,
            IEnumerable<PackageRegistration> packageRegistrations = null,
            IEnumerable<PackageDependency> packageDependencies = null,
            IEnumerable<Package> packages = null,
            IEnumerable<Certificate> certificates = null)
        {
            context.SetupDbSet(c => c.PackageRegistrations, packageRegistrationsMock, packageRegistrations);
            context.SetupDbSet(c => c.Set<PackageDependency>(), packageDependenciesMock, packageDependencies);
            context.SetupDbSet(c => c.Set<Package>(), packagesMock, packages);
            context.SetupDbSet(c => c.Certificates, certificatesMock, certificates);
        }
    }
}
