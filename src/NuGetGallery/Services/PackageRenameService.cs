// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Data.Entity;
using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class PackageRenameService : IPackageRenameService
    {
        private readonly IEntityRepository<PackageRename> _packageRenameRepository;

        public PackageRenameService (
            IEntityRepository<PackageRename> packageRenameRepository)
        {
            _packageRenameRepository = packageRenameRepository ?? throw new ArgumentNullException(nameof(packageRenameRepository));
        }

        public IReadOnlyList<PackageRename> GetPackageRenames(PackageRegistration packageRegistration)
        {
            return _packageRenameRepository.GetAll()
                .Where(pr => pr.FromPackageRegistrationKey == packageRegistration.Key)
                .Include(pr => pr.ToPackageRegistration)
                .ToList();
        }
    }
}