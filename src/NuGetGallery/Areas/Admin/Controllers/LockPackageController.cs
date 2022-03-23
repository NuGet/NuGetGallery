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
using NuGetGallery.Auditing;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class LockPackageController : AdminControllerBase
    {
        private readonly IEntityRepository<PackageRegistration> _packageRegistrationRepository;
        private readonly IAuditingService _auditingService;

        public LockPackageController(
            IEntityRepository<PackageRegistration> packageRegistrationRepository,
            IAuditingService auditingService)
        {
            _packageRegistrationRepository = packageRegistrationRepository ?? throw new ArgumentNullException(nameof(packageRegistrationRepository));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            var model = new LockPackageViewModel();

            return View("LockIndex", model);
        }

        [HttpGet]
        public virtual ActionResult Search(string query)
        {
            var lines = Helpers.ParseQueryToLines(query);
            var packageRegistrations = GetPackageRegistrationsForIds(lines);

            return View("LockIndex", new LockPackageViewModel
            {
                Query = query,
                LockStates = packageRegistrations
                    .Select(x => new LockState { Identifier = x.Id, IsLocked = x.IsLocked })
                    .ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Update(LockPackageViewModel viewModel)
        {
            var counter = 0;
            viewModel = viewModel ?? new LockPackageViewModel();

            if (viewModel.LockStates != null)
            {
                var packageIdsFromRequest = viewModel.LockStates.Select(x => x.Identifier).ToList();
                var packageRegistrationsFromDb = GetPackageRegistrationsForIds(packageIdsFromRequest);

                var packageStatesFromRequestDictionary = viewModel
                    .LockStates
                    .ToDictionary(x => x.Identifier, StringComparer.OrdinalIgnoreCase);

                foreach (var packageRegistration in packageRegistrationsFromDb)
                {
                    if (packageStatesFromRequestDictionary.TryGetValue(packageRegistration.Id, out var packageStateRequest))
                    {
                        if (packageRegistration.IsLocked != packageStateRequest.IsLocked)
                        {
                            packageRegistration.IsLocked = packageStateRequest.IsLocked;
                            counter++;
                            await _auditingService.SaveAuditRecordAsync(new PackageRegistrationAuditRecord(
                                packageRegistration,
                                packageStateRequest.IsLocked ? AuditedPackageRegistrationAction.Lock : AuditedPackageRegistrationAction.Unlock,
                                owner: null));
                        }
                    }
                }

                if (counter > 0)
                {
                    await _packageRegistrationRepository.CommitChangesAsync();
                }
            }

            TempData["Message"] = string.Format(CultureInfo.InvariantCulture, $"Lock state was updated for {counter} packages.");

            return View("LockIndex", viewModel);
        }

        private IList<PackageRegistration> GetPackageRegistrationsForIds(IReadOnlyList<string> ids)
        {
            return _packageRegistrationRepository.GetAll().Where(x => ids.Contains(x.Id)).ToList();
        }
    }
}