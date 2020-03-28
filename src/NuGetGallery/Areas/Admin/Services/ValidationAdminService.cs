// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Areas.Admin.Services
{
    public class ValidationAdminService
    {
        private readonly IEntityRepository<PackageValidationSet> _validationSets;
        private readonly IEntityRepository<PackageValidation> _validations;
        private readonly IEntityRepository<Package> _packages;
        private readonly IEntityRepository<SymbolPackage> _symbolPackages;

        public ValidationAdminService(
            IEntityRepository<PackageValidationSet> validationSets,
            IEntityRepository<PackageValidation> validations,
            IEntityRepository<Package> packages,
            IEntityRepository<SymbolPackage> symbolPackages)
        {
            _validationSets = validationSets ?? throw new ArgumentNullException(nameof(validationSets));
            _validations = validations ?? throw new ArgumentNullException(nameof(validations));
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _symbolPackages = symbolPackages ?? throw new ArgumentNullException(nameof(symbolPackages));
        }

        /// <summary>
        /// Fetch a list of package validation sets matching the provided query. The query is a line seperated list of
        /// identifiers. These identifiers can refer to packages, sets, or validations. An empty list is returned if no
        /// sets are matched.
        /// </summary>
        public IReadOnlyList<PackageValidationSet> Search(string query)
        {
            var lines = Helpers.ParseQueryToLines(query);

            // Search for matching validation sets using various methods of parsing the lines.
            var validationSets = new Dictionary<long, PackageValidationSet>();
            foreach (var line in lines)
            {
                SearchByValidationSetTrackingId(validationSets, line);
                SearchByValidationId(validationSets, line);
                SearchByValidationSetKey(validationSets, line);
                SearchByPackageIdAndVersion(validationSets, line);
                SearchByPackageId(validationSets, line);
            }

            return validationSets
                .Values
                .ToList();
        }

        /// <summary>
        /// Fetch the list of pending package validation sets.
        /// </summary>
        public IReadOnlyList<PackageValidationSet> Pending()
        {
            var pendingPackages = _packages
                .GetAll()
                .Where(p => p.PackageStatusKey == PackageStatus.Validating)
                .Select(p => p.Key)
                .ToList();
            var pendingSymbolPackages = _symbolPackages
                .GetAll()
                .Where(s => s.StatusKey == PackageStatus.Validating)
                .Select(s => s.Key)
                .ToList();

            return _validationSets
                .GetAll()
                .Where(v =>
                    (v.ValidatingType == ValidatingType.Package && pendingPackages.Contains(v.PackageKey)) ||
                    (v.ValidatingType == ValidatingType.SymbolPackage && pendingSymbolPackages.Contains(v.PackageKey)))
                .Include(v => v.PackageValidations)
                .ToList();
        }


        public PackageDeletedStatus GetDeletedStatus(int key, ValidatingType validatingType)
        {
            switch (validatingType)
            {
                case ValidatingType.Package:
                    return GetPackageDeletedStatus(key);
                case ValidatingType.SymbolPackage:
                    return GetSymbolPackageDeletedStatus(key);
                default:
                    return PackageDeletedStatus.Unknown;
            }
        }

        /// <summary>
        /// Determines if deleted status of the provided package key. This method is unable to differentiate between
        /// a hard deleted package and a package that never existed in the first place. Therefore,
        /// <see cref="PackageDeletedStatus.Unknown"/> is returned if the package key is not found.
        /// </summary>
        public PackageDeletedStatus GetPackageDeletedStatus(int packageKey)
        {
            var package = _packages
                .GetAll()
                .Where(x => x.Key == packageKey)
                .FirstOrDefault();

            if (package == null)
            {
                return PackageDeletedStatus.Unknown;
            }
            else if (package.PackageStatusKey == PackageStatus.Deleted)
            {
                return PackageDeletedStatus.SoftDeleted;
            }

            return PackageDeletedStatus.NotDeleted;
        }

        public PackageDeletedStatus GetSymbolPackageDeletedStatus(int symbolPackageKey)
        {
            var symbolPackage = _symbolPackages
                .GetAll()
                .Where(x => x.Key == symbolPackageKey)
                .FirstOrDefault();

            if (symbolPackage == null)
            {
                return PackageDeletedStatus.Unknown;
            }
            else if (symbolPackage.StatusKey == PackageStatus.Deleted)
            {
                return PackageDeletedStatus.SoftDeleted;
            }

            return PackageDeletedStatus.NotDeleted;
        }

        private void SearchByValidationSetTrackingId(Dictionary<long, PackageValidationSet> validationSets, string line)
        {
            if (Guid.TryParse(line, out Guid guid))
            {
                var validationSet = _validationSets
                    .GetAll()
                    .Include(x => x.PackageValidations)
                    .Where(x => x.ValidationTrackingId == guid)
                    .FirstOrDefault();

                if (validationSet != null)
                {
                    validationSets[validationSet.Key] = validationSet;
                }
            }
        }

        private void SearchByValidationId(Dictionary<long, PackageValidationSet> validationSets, string line)
        {
            if (Guid.TryParse(line, out Guid guid))
            {
                var validation = _validations
                    .GetAll()
                    .Include(x => x.PackageValidationSet)
                    .Where(x => x.Key == guid)
                    .FirstOrDefault();

                if (validation != null)
                {
                    validationSets[validation.PackageValidationSet.Key] = validation.PackageValidationSet;
                }
            }
        }

        private void SearchByValidationSetKey(Dictionary<long, PackageValidationSet> validationSets, string line)
        {
            if (long.TryParse(line, out long integer))
            {
                var validationSet = _validationSets
                    .GetAll()
                    .Include(x => x.PackageValidations)
                    .Where(x => x.Key == integer)
                    .FirstOrDefault();

                if (validationSet != null)
                {
                    validationSets[validationSet.Key] = validationSet;
                }
            }
        }

        private void SearchByPackageIdAndVersion(Dictionary<long, PackageValidationSet> validationSets, string line)
        {
            if (line.Contains(' '))
            {
                var pieces = line.Split(' ');
                NuGetVersion version;
                if (NuGetVersion.TryParse(pieces[1], out version))
                {
                    var normalizedVersion = version.ToNormalizedString();
                    var id = pieces[0];
                    var matchedSets = _validationSets
                        .GetAll()
                        .Include(x => x.PackageValidations)
                        .Where(x => x.PackageId == id && x.PackageNormalizedVersion == normalizedVersion)
                        .ToList();

                    foreach (var validationSet in matchedSets)
                    {
                        validationSets[validationSet.Key] = validationSet;
                    }
                }
            }
        }

        private void SearchByPackageId(Dictionary<long, PackageValidationSet> validationSets, string line)
        {
            var matchedSets = _validationSets
                .GetAll()
                .Include(x => x.PackageValidations)
                .Where(x => x.PackageId == line)
                .ToList();

            foreach (var validationSet in matchedSets)
            {
                validationSets[validationSet.Key] = validationSet;
            }
        }
    }
}