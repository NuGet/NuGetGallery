// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class CoreSymbolPackageService : ICoreSymbolPackageService
    {
        protected readonly IEntityRepository<SymbolPackage> _symbolPackageRepository;
        protected readonly ICorePackageService _corePackageService;

        public CoreSymbolPackageService(
            IEntityRepository<SymbolPackage> symbolPackageRepository,
            ICorePackageService corePackageService)
        {
            _symbolPackageRepository = symbolPackageRepository ?? throw new ArgumentNullException(nameof(symbolPackageRepository));
            _corePackageService = corePackageService ?? throw new ArgumentNullException(nameof(corePackageService));
        }

        public IEnumerable<SymbolPackage> FindSymbolPackagesByIdAndVersion(string id, string version)
        {
            var package = _corePackageService.FindPackageByIdAndVersionStrict(id, version);

            return package?.SymbolPackages;
        }

        public virtual async Task UpdateStatusAsync(SymbolPackage symbolPackage, PackageStatus newPackageStatus, bool commitChanges = true)
        {
            if (symbolPackage == null)
            {
                throw new ArgumentNullException(nameof(symbolPackage));
            }

            // Avoid all of this work if the package status is not changing.
            if (symbolPackage.StatusKey != newPackageStatus)
            {
                symbolPackage.StatusKey = newPackageStatus;

                /// If the package is being made available.
                if (newPackageStatus == PackageStatus.Available)
                {
                    symbolPackage.Published = DateTime.UtcNow;
                }

                if (commitChanges)
                {
                    await _symbolPackageRepository.CommitChangesAsync();
                }
            }
        }
    }
}