// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class LockPackageController : AdminControllerBase
    {
        private IEntityRepository<PackageRegistration> _packageRegistrationRepository;

        public LockPackageController(IEntityRepository<PackageRegistration> packageRegistrationRepository)
        {
            _packageRegistrationRepository = packageRegistrationRepository ?? throw new ArgumentNullException(nameof(packageRegistrationRepository));
        }

        [HttpGet]
        [ActionName(ActionName.AdminLockPackageIndex)]
        public virtual ActionResult Index()
        {
            var model = new LockPackageViewModel();

            return View(model);
        }

        [HttpGet]
        [ActionName(ActionName.AdminLockPackageSearch)]
        public virtual ActionResult Search(string query)
        {
            var lines = Helpers.ParseQueryToLines(query);
            var packageRegistrations = GetPackageRegistrationsForIds(lines);

            return View(nameof(Index), new LockPackageViewModel()
            {
                Query = query,
                PackageLockStates = packageRegistrations.Select(x => new PackageLockState() { Id = x.Id, IsLocked = x.IsLocked }).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName(ActionName.AdminLockPackageUpdate)]
        public async Task<ActionResult> Update(LockPackageViewModel lockPackageViewModel)
        {
            int counter = 0;

            if (lockPackageViewModel != null && lockPackageViewModel.PackageLockStates != null)
            {
                var packageIdsFromRequest = lockPackageViewModel.PackageLockStates.Select(x => x.Id).ToList();
                var packageRegistrationsFromDb = GetPackageRegistrationsForIds(packageIdsFromRequest);

                var packageStatesFromRequestDictionary = lockPackageViewModel.PackageLockStates.ToDictionary(x => x.Id);

                foreach (var packageRegistration in packageRegistrationsFromDb)
                {
                    if (packageStatesFromRequestDictionary.TryGetValue(packageRegistration.Id, out var packageStateRequest))
                    {
                        if (packageRegistration.IsLocked != packageStateRequest.IsLocked)
                        {
                            packageRegistration.IsLocked = packageStateRequest.IsLocked;
                            counter++;
                        }
                    }
                }

                if (counter > 0)
                {
                    await _packageRegistrationRepository.CommitChangesAsync();
                }
            }

            TempData["Message"] = string.Format(CultureInfo.InvariantCulture, $"Lock state was updated for {counter} packages.");

            return View(nameof(Index), lockPackageViewModel);
        }

        private IList<PackageRegistration> GetPackageRegistrationsForIds(IReadOnlyList<string> ids)
        {
            return _packageRegistrationRepository.GetAll().Where(x => ids.Contains(x.Id)).ToList();
        }
    }
}