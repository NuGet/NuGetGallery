// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NuGet.Services.Validation;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin.Services;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ValidationController : AdminControllerBase
    {
        private readonly ValidationAdminService _validationAdminService;

        public ValidationController(ValidationAdminService validationAdminService)
        {
            _validationAdminService = validationAdminService ?? throw new ArgumentNullException(nameof(validationAdminService));
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            return View(nameof(Index), new ValidationPageViewModel());
        }

        [HttpGet]
        public virtual ActionResult Pending()
        {
            var validationSets = _validationAdminService.GetPending();
            var validatedPackages = ToValidatedPackages(validationSets);
            var validationSetIds = validatedPackages
                .SelectMany(p => p.ValidationSets)
                .Select(s => s.ValidationTrackingId);
            var query = string.Join("\r\n", validationSetIds);

            return View(nameof(Index), new ValidationPageViewModel(query, validatedPackages));
        }

        [HttpGet]
        public virtual ActionResult Search(string q)
        {
            var packageValidationSets = _validationAdminService.Search(q ?? string.Empty);
            var validatedPackages = ToValidatedPackages(packageValidationSets);

            return View(nameof(Index), new ValidationPageViewModel(q, validatedPackages));
        }

        private List<ValidatedPackageViewModel> ToValidatedPackages(IReadOnlyList<PackageValidationSet> packageValidationSets)
        {
            var validatedPackages = new List<ValidatedPackageViewModel>();
            AppendValidatedPackages(validatedPackages, packageValidationSets, ValidatingType.Package);
            AppendValidatedPackages(validatedPackages, packageValidationSets, ValidatingType.SymbolPackage);

            return validatedPackages;
        }

        private void AppendValidatedPackages(List<ValidatedPackageViewModel> validatedPackages, IEnumerable<PackageValidationSet> validationSets, ValidatingType validatingType)
        {
            var groups = validationSets
                .Where(x => x.ValidatingType == validatingType)
                .OrderBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(x => NuGetVersion.Parse(x.PackageNormalizedVersion))
                .ThenByDescending(x => x.PackageKey)
                .ThenByDescending(x => x.Created)
                .ThenByDescending(x => x.Key)
                .GroupBy(x => x.PackageKey);

            foreach (var group in groups)
            {
                foreach (var set in group)
                {
                    set.PackageValidations = set.PackageValidations
                        .OrderBy(x => x.Started)
                        .ToList();
                }
                var deletedStatus = _validationAdminService.GetDeletedStatus(group.Key, validatingType);
                var validatedPackage = new ValidatedPackageViewModel(group.ToList(), deletedStatus, validatingType);
                validatedPackages.Add(validatedPackage);
            }
        }
    }
}