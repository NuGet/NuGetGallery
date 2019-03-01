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
        [ActionName(ActionName.AdminValidationIndex)]
        public virtual ActionResult Index()
        {
            return View(nameof(Index), new ValidationPageViewModel());
        }

        [HttpGet]
        [ActionName(ActionName.AdminValidationSearch)]
        public virtual ActionResult Search(string q)
        {
            var packageValidationSets = _validationAdminService.Search(q ?? string.Empty);
            var validatedPackages = new List<ValidatedPackageViewModel>();
            AppendValidatedPackages(validatedPackages, packageValidationSets, ValidatingType.Package);
            AppendValidatedPackages(validatedPackages, packageValidationSets, ValidatingType.SymbolPackage);

            return View(nameof(Index), new ValidationPageViewModel(q, validatedPackages));
        }

        private void AppendValidatedPackages(List<ValidatedPackageViewModel> validatedPackages, IEnumerable<PackageValidationSet> validationSets, ValidatingType validatingType)
        {
            var groups = validationSets
                .Where(x => x.ValidatingType == validatingType)
                .OrderBy(x => x.PackageId)
                .ThenByDescending(x => NuGetVersion.Parse(x.PackageNormalizedVersion))
                .ThenByDescending(x => x.PackageKey)
                .ThenByDescending(x => x.Created)
                .ThenByDescending(x => x.Key)
                .GroupBy(x => x.PackageKey);

            foreach (var group in groups)
            {
                foreach (var set in group)
                {
                    // Put completed validations first then put new validations on top.
                    set.PackageValidations = set.PackageValidations
                        .OrderByDescending(x => x.ValidationStatus)
                        .ThenByDescending(x => x.ValidationStatusTimestamp)
                        .ToList();
                }
                var deletedStatus = _validationAdminService.GetDeletedStatus(group.Key, validatingType);
                var validatedPackage = new ValidatedPackageViewModel(group.ToList(), deletedStatus, validatingType);
                validatedPackages.Add(validatedPackage);
            }
        }
    }
}