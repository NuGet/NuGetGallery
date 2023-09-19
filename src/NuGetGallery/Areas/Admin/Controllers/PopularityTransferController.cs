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
                                        .Select(id => _packageService.FindPackageRegistrationById(id.Trim()))
                                        .ToList();
            var packagesTo = packagesToInput
                                        .Split(null) // all whitespace
                                        .Where(id => id != string.Empty)
                                        .Select(id => _packageService.FindPackageRegistrationById(id.Trim()))
                                        .ToList();

            if (packagesFrom.Count != packagesTo.Count)
            {
                return Json(HttpStatusCode.BadRequest, "There must be an equal number of Package IDs in the 'From' and 'To' fields.", JsonRequestBehavior.AllowGet);
            }

            var result = new PopularityTransferViewModel();

            for (int i = 0; i < packagesFrom.Count; i++)
            {
                var packageFrom = packagesFrom[i];
                var packageTo = packagesTo[i];

                if (packageFrom is null)
                {
                    var InvalidId = packagesFromInput
                                                    .Split(null)
                                                    .Where(id => id != string.Empty)
                                                    .ToList()[i].Trim();
                    return Json(HttpStatusCode.BadRequest,
                                $"Could not find a package with the Package ID: {InvalidId}",
                                JsonRequestBehavior.AllowGet);
                }
                if (packageTo is null)
                {
                    var InvalidId = packagesToInput
                                                    .Split(null)
                                                    .Where(id => id != string.Empty)
                                                    .ToList()[i].Trim();
                    return Json(HttpStatusCode.BadRequest,
                                $"Could not find a package with the Package ID: {InvalidId}",
                                JsonRequestBehavior.AllowGet);
                }

                var input = new PopularityTransferItem(CreatePackageSearchResult(packageFrom.Packages.First()),
                                                       CreatePackageSearchResult(packageTo.Packages.First()),
                                                       packageFrom.Key,
                                                       packageTo.Key);

                result.ValidatedInputs.Add(input);

                // check for existing entries in the PackageRename table for the 'From' packages
                var existingRenames = _packageRenameService.GetPackageRenames(packageFrom);

                if (existingRenames.Any())
                {
                    if (existingRenames.Count() == 1)
                    {
                        var existingRenamesMessage = $"{packageFrom.Id} already has 1 entry in the PackageRenames table. This will be removed with this operation.";
                        result.ExistingPackageRenames.Add(existingRenamesMessage);
                    }
                    else
                    {
                        var existingRenamesMessage = $"{packageFrom.Id} already has {existingRenames.Count()} entries in the PackageRenames table. These will be removed with this operation.";
                        result.ExistingPackageRenames.Add(existingRenamesMessage);
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

                if (input.FromId.Length > maxIdLength)
                {
                    maxIdLength = input.FromId.Length;
                }
            }

            var result = new PopularityTransferViewModel();

            _packageRenameRepository.DeleteOnCommit(previousPackageRenames);
            _packageRenameRepository.InsertOnCommit(newPackageRenames);
            await _packageRenameRepository.CommitChangesAsync();

            await _entitiesContext.SaveChangesAsync();

            result.SuccessMessage = GenerateSuccessMessage(confirmedInputs, maxIdLength);

            return Json(HttpStatusCode.OK, result, JsonRequestBehavior.AllowGet);
        }

        private string GenerateSuccessMessage(List<PopularityTransferItem> confirmedInputs, int maxIdLength)
        {
            var message = "Popularity transfer(s) executed successfully:\n\n";

            foreach (var input in confirmedInputs)
            {
                message += $"\t{input.FromId.PadRight(maxIdLength)}  --->  {input.ToId}\n";
            }

            message += "\nIt can take up to 30 minutes for the popularity transfer(s) to be applied.";

            return message;
        }
    }
}