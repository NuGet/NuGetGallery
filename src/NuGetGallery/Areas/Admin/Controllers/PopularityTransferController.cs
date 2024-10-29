// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class PopularityTransferController : AdminControllerBase
    {
        private readonly IPackageService _packageService;
        private readonly IPackageRenameService _packageRenameService;
        private readonly IEntityRepository<PackageRename> _packageRenameRepository;
        private readonly IEntitiesContext _entitiesContext;

        public PopularityTransferController(
            IPackageService packageService,
            IPackageRenameService packageRenameService,
            IEntityRepository<PackageRename> packageRenameRepository,
            IEntitiesContext entitiesContext)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageRenameService = packageRenameService ?? throw new ArgumentNullException(nameof(packageRenameService));
            _packageRenameRepository = packageRenameRepository ?? throw new ArgumentNullException(nameof(packageRenameRepository));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        [HttpGet]
        public ViewResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult ValidateInputs(string packagesFromInput, string packagesToInput)
        {
            if (string.IsNullOrEmpty(packagesFromInput) || string.IsNullOrEmpty(packagesToInput))
            {
                return Json(HttpStatusCode.BadRequest, "Package IDs in the 'From' or 'To' fields cannot be null or empty.", JsonRequestBehavior.AllowGet);
            }

            var packagesFrom = packagesFromInput
                                        .Split(null) // all whitespace
                                        .Where(id => id != string.Empty)
                                        .Select(id => id.Trim())
                                        .ToList();
            var packagesTo = packagesToInput
                                        .Split(null) // all whitespace
                                        .Where(id => id != string.Empty)
                                        .Select(id => id.Trim())
                                        .ToList();

            if (packagesFrom.Count != packagesTo.Count)
            {
                return Json(HttpStatusCode.BadRequest, "There must be an equal number of Package IDs in the 'From' and 'To' fields.", JsonRequestBehavior.AllowGet);
            }

            var result = new PopularityTransferViewModel();
            HashSet<int> inputKeys = new HashSet<int>();

            for (int i = 0; i < packagesFrom.Count; i++)
            {
                var packageFrom = _packageService.FindPackageRegistrationById(packagesFrom[i]);
                var packageTo = _packageService.FindPackageRegistrationById(packagesTo[i]);

                // check for invalid package ids
                if (packageFrom is null || !packageFrom.Packages.Any())
                {
                    return Json(HttpStatusCode.BadRequest,
                                $"Could not find a package with the Package ID: {packagesFrom[i]}",
                                JsonRequestBehavior.AllowGet);
                }
                if (packageTo is null || !packageTo.Packages.Any())
                {
                    return Json(HttpStatusCode.BadRequest,
                                $"Could not find a package with the Package ID: {packagesTo[i]}",
                                JsonRequestBehavior.AllowGet);
                }

                // check for duplicate package ids
                if (!inputKeys.Add(packageFrom.Key))
                {
                    return Json(HttpStatusCode.BadRequest,
                                $"{packageFrom.Id} appears twice. Please remove duplicate Package IDs from the 'From' and 'To' fields.",
                                JsonRequestBehavior.AllowGet);
                }
                if (!inputKeys.Add(packageTo.Key))
                {
                    return Json(HttpStatusCode.BadRequest,
                                $"{packageTo.Id} appears twice. Please remove duplicate Package IDs from the 'From' and 'To' fields.",
                                JsonRequestBehavior.AllowGet);
                }

                // create validated input result
                result.ValidatedInputs.Add(new PopularityTransferItem(packageFrom, packageTo));

                // checking for existing entries in the PackageRenames table
                // 1. 'From' input that already has a 'From' entry in the PackageRenames table -- Conflict 
                var existingRenames = _packageRenameService.GetPackageRenames(packageFrom);

                if (existingRenames.Any())
                {
                    if (existingRenames.Count == 1)
                    {
                        result.ExistingPackageRenamesMessagesConflict.Add($"{packageFrom.Id} already has 1 entry in the PackageRenames table. This will be removed with this operation.");
                    }
                    else
                    {
                        result.ExistingPackageRenamesMessagesConflict.Add($"{packageFrom.Id} already has {existingRenames.Count} entries in the PackageRenames table. These will be removed with this operation.");
                    }

                    foreach (var existingRename in existingRenames)
                    {
                        result.ExistingPackageRenamesConflict.Add(new PopularityTransferItem(existingRename.FromPackageRegistration, existingRename.ToPackageRegistration));
                    }
                }

                // 2. 'From' input that already has a 'To' entry in the PackageRenames table -- Transitive
                existingRenames = _packageRenameService.GetPackageRenamesTo(packageFrom);

                if (existingRenames.Any())
                {
                    var existingRenamesMessage = $"{packageFrom.Id} already has entries in the PackageRenames table. This popularity transfer will result in a new transitive relationship. Please look at the PackageRenames table and verify your input before proceeding.";
                    result.ExistingPackageRenamesMessagesTransitive.Add(existingRenamesMessage);

                    foreach (var existingRename in existingRenames)
                    {
                        result.ExistingPackageRenamesTransitive.Add(new PopularityTransferItem(existingRename.FromPackageRegistration, existingRename.ToPackageRegistration));
                    }
                }

                // 3. 'To' input that already has a 'From' entry in the PackageRenames table -- Transitive
                existingRenames = _packageRenameService.GetPackageRenames(packageTo);

                if (existingRenames.Any())
                {
                    var existingRenamesMessage = $"{packageTo.Id} already has entries in the PackageRenames table. This popularity transfer will result in a new transitive relationship. Please look at the PackageRenames table and verify your input before proceeding.";
                    result.ExistingPackageRenamesMessagesTransitive.Add(existingRenamesMessage);

                    foreach (var existingRename in existingRenames)
                    {
                        result.ExistingPackageRenamesTransitive.Add(new PopularityTransferItem(existingRename.FromPackageRegistration, existingRename.ToPackageRegistration));
                    }
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExecutePopularityTransfer(List<PopularityTransferItem> confirmedInputs)
        {
            var newPackageRenames = new List<PackageRename>();
            var previousPackageRenames = new List<PackageRename>();
            
            // keeping track of this so we can pad the success message
            int maxIdLength = 0;

            foreach (var input in confirmedInputs)
            {
                newPackageRenames.Add(new PackageRename
                                        {
                                            FromPackageRegistrationKey = input.FromKey,
                                            ToPackageRegistrationKey = input.ToKey,
                                            TransferPopularity = true
                                        });

                var previousRenames = _packageRenameService.GetPackageRenames(_packageService.FindPackageRegistrationById(input.FromId));
                previousPackageRenames.AddRange(previousRenames);

                maxIdLength = Math.Max(maxIdLength, input.FromId.Length);
            }

            var result = new PopularityTransferViewModel();

            _packageRenameRepository.DeleteOnCommit(previousPackageRenames);
            _packageRenameRepository.InsertOnCommit(newPackageRenames);

            await _entitiesContext.SaveChangesAsync();

            result.SuccessMessage = GenerateSuccessMessage(confirmedInputs, maxIdLength);

            return Json(HttpStatusCode.OK, result, JsonRequestBehavior.AllowGet);
        }

        private string GenerateSuccessMessage(List<PopularityTransferItem> confirmedInputs, int maxIdLength)
        {
            var message = "Popularity transfer(s) executed successfully:\n\n";

            foreach (var input in confirmedInputs)
            {
                message += $"\t{input.FromId.PadRight(maxIdLength)}  ➡️  {input.ToId}\n";
            }

            message += "\nIt can take up to 30 minutes for the popularity transfer(s) to be applied.";

            return message;
        }
    }
}