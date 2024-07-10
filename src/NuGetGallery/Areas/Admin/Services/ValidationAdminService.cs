// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Areas.Admin.Services
{
    public class ValidationAdminService
    {
        private const int PendingValidationsBatchSize = 100;

        private readonly IEntityRepository<PackageValidationSet> _validationSets;
        private readonly IEntityRepository<PackageValidation> _validations;
        private readonly IEntityRepository<Package> _packages;
        private readonly IEntityRepository<SymbolPackage> _symbolPackages;
        private readonly IValidationService _validationService;

        public ValidationAdminService(
            IEntityRepository<PackageValidationSet> validationSets,
            IEntityRepository<PackageValidation> validations,
            IEntityRepository<Package> packages,
            IEntityRepository<SymbolPackage> symbolPackages,
            IValidationService validationService)
        {
            _validationSets = validationSets ?? throw new ArgumentNullException(nameof(validationSets));
            _validations = validations ?? throw new ArgumentNullException(nameof(validations));
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _symbolPackages = symbolPackages ?? throw new ArgumentNullException(nameof(symbolPackages));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
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
        /// Fetch the list of validation sets whose packages are in the "validating" status.
        /// </summary>
        public IReadOnlyList<PackageValidationSet> GetPending()
        {
            var pendingValidations = new List<PackageValidationSet>();
            var pendingPackages = _packages
                .GetAll()
                .Where(p => p.PackageStatusKey == PackageStatus.Validating)
                .Select(p => (int?)p.Key)
                .ToList();
            var pendingSymbolPackages = _symbolPackages
                .GetAll()
                .Where(s => s.StatusKey == PackageStatus.Validating)
                .Select(s => (int?)s.Key)
                .ToList();

            // TODO: Add generic validation sets.
            // Tracked by: https://github.com/NuGet/Engineering/issues/3587
            AddPendingValidationSets(pendingValidations, pendingPackages, ValidatingType.Package);
            AddPendingValidationSets(pendingValidations, pendingSymbolPackages, ValidatingType.SymbolPackage);

            return pendingValidations;
        }

        public async Task<int> RevalidatePendingAsync(ValidatingType validatingType)
        {
            if (validatingType == ValidatingType.Package)
            {
                var pendingPackages = _packages
                    .GetAll()
                    .Include(p => p.PackageRegistration)
                    .Where(p => p.PackageStatusKey == PackageStatus.Validating)
                    .ToList();

                foreach (var package in pendingPackages)
                {
                    await _validationService.RevalidateAsync(package);
                }

                return pendingPackages.Count;
            }
            else if (validatingType == ValidatingType.SymbolPackage)
            {
                var pendingSymbolPackages = _symbolPackages
                    .GetAll()
                    .Include(p => p.Package)
                    .Include(p => p.Package.PackageRegistration)
                    .Where(s => s.StatusKey == PackageStatus.Validating)
                    .ToList();

                foreach (var symbolPackage in pendingSymbolPackages)
                {
                    await _validationService.RevalidateAsync(symbolPackage);
                }

                return pendingSymbolPackages.Count;
            }
            else
            {
                throw new NotSupportedException("The validating type " + validatingType + " is not supported.");
            }
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
                .FirstOrDefault(x => x.Key == packageKey);

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
                .FirstOrDefault(x => x.Key == symbolPackageKey);

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
                    .FirstOrDefault(x => x.ValidationTrackingId == guid);

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
                    .FirstOrDefault(x => x.Key == guid);

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
                    .FirstOrDefault(x => x.Key == integer);

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

        private void AddPendingValidationSets(
            List<PackageValidationSet> validationSets,
            IReadOnlyList<int?> packageKeys,
            ValidatingType type)
        {
            foreach (var packageKeyBatch in Batch(packageKeys, PendingValidationsBatchSize))
            {
                validationSets.AddRange(
                    _validationSets
                        .GetAll()
                        .Where(v => v.ValidatingType == type)
                        .Where(v => packageKeyBatch.Contains(v.PackageKey)));
            }
        }

        private IEnumerable<IEnumerable<TElement>> Batch<TElement>(IReadOnlyList<TElement> input, int size)
        {
            for (var i = 0; i < input.Count; i += size)
            {
                yield return input.Skip(i).Take(size);
            }
        }
    }
}