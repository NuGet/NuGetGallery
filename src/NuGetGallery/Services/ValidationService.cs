// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;

namespace NuGetGallery
{
    public class ValidationService : IValidationService
    {
        private readonly IPackageService _packageService;
        private readonly IPackageValidationInitiator _initiator;
        private readonly IValidationEntitiesContext _validationContext;


        public ValidationService(
            IPackageService packageService,
            IPackageValidationInitiator initiator,
            ValidationEntitiesContext validationContext)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _initiator = initiator ?? throw new ArgumentNullException(nameof(initiator));
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
        }

        public async Task StartValidationAsync(Package package)
        {
            var packageStatus = await _initiator.StartValidationAsync(package);

            await _packageService.UpdatePackageStatusAsync(
                package,
                packageStatus,
                commitChanges: false);
        }

        public async Task RevalidateAsync(Package package)
        {
            await _initiator.StartValidationAsync(package);
        }

        public async Task<IEnumerable<ValidationIssue>> GetLatestValidationIssuesAsync(Package package)
        {
            // Only query the database for validation issues if the package has failed validation.
            if (package.PackageStatusKey == PackageStatus.FailedValidation)
            {
                // Grab the most recent failed validation set for this package. There should always
                // be a failed validation set for a package whose validation has failed.
                var validationSet = await _validationContext
                            .PackageValidationSets
                            .OrderByDescending(s => s.Updated)
                            .Where(s => s.PackageValidations.Any(v => v.ValidationStatus == ValidationStatus.Failed))
                            .Include(s => s.PackageValidations.Select(v => v.PackageValidationIssues))
                            .FirstOrDefaultAsync();

                if (validationSet != null)
                {
                    // Get the failed validation set's validation issues. The issues are ordered by their
                    // key so that it appears that issues are appended as more validations fail.
                    return validationSet
                                .PackageValidations
                                .SelectMany(v => v.PackageValidationIssues)
                                .OrderBy(i => i.Key)
                                .Select(i => ValidationIssue.Deserialize(i.IssueCode, i.Data))
                                .ToList();
                }
            }

            return Enumerable.Empty<ValidationIssue>();
        }
    }
}