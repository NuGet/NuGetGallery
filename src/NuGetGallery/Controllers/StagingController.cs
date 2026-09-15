// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Issues;
using NuGetGallery.Filters;

namespace NuGetGallery
{
    /// <summary>
    /// Handles signed-in owner management of private staged packages.
    /// </summary>
    [UIAuthorize]
    public class StagingController : AppController
    {
        private readonly IPackageStagingAuthorizationService _packageStagingAuthorizationService;
        private readonly IPackageStagingManagementService _packageStagingManagementService;
        private readonly IPackageStagingPromotionService _packageStagingPromotionService;
        private readonly IPackageStagingUploadService _packageStagingUploadService;
        private readonly IValidationService _validationService;

        public StagingController(
            IPackageStagingAuthorizationService packageStagingAuthorizationService,
            IPackageStagingManagementService packageStagingManagementService,
            IPackageStagingPromotionService packageStagingPromotionService,
            IPackageStagingUploadService packageStagingUploadService,
            IValidationService validationService)
        {
            _packageStagingAuthorizationService = packageStagingAuthorizationService ?? throw new ArgumentNullException(nameof(packageStagingAuthorizationService));
            _packageStagingManagementService = packageStagingManagementService ?? throw new ArgumentNullException(nameof(packageStagingManagementService));
            _packageStagingPromotionService = packageStagingPromotionService ?? throw new ArgumentNullException(nameof(packageStagingPromotionService));
            _packageStagingUploadService = packageStagingUploadService ?? throw new ArgumentNullException(nameof(packageStagingUploadService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        }

        [HttpGet]
        public virtual ActionResult CreateGroup()
        {
            var owners = GetStagingOwnerNames();
            if (owners.Count == 0)
            {
                return HttpNotFound();
            }

            return View(new CreateStagingGroupViewModel
            {
                Owner = owners.SingleOrDefault(owner => string.Equals(owner, GetCurrentUser().Username, StringComparison.OrdinalIgnoreCase)) ?? owners[0],
                Owners = owners,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> CreateGroup(CreateStagingGroupViewModel model)
        {
            var owners = GetStagingOwnerNames();
            model.Owners = owners;

            if (!string.IsNullOrWhiteSpace(model.Owner) && !owners.Contains(model.Owner, StringComparer.OrdinalIgnoreCase))
            {
                return HttpNotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var stagingOwner = _packageStagingAuthorizationService.GetEnabledOwner(GetCurrentUser(), model.Owner);
            if (stagingOwner == null)
            {
                return HttpNotFound();
            }

            var result = await _packageStagingManagementService.CreateStagingGroupAsync(stagingOwner, model.Id, model.Name);
            switch (result.Type)
            {
                case CreateStagingGroupResultType.Created:
                    return Redirect(Url.ManageMyStagingGroups());

                case CreateStagingGroupResultType.GroupAlreadyExists:
                    ModelState.AddModelError(nameof(model.Id), "A staging group with this ID already exists.");
                    return View(model);

                default:
                    throw new InvalidOperationException($"Unknown staging group creation result: {result.Type}.");
            }
        }

        [HttpGet]
        public virtual ActionResult Group(string owner, string groupId)
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(groupId))
            {
                return HttpNotFound();
            }

            var currentUser = GetCurrentUser();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledOwner(currentUser, owner);
            if (stagingOwner == null)
            {
                return HttpNotFound();
            }

            var group = _packageStagingManagementService.FindStagingGroup(stagingOwner, groupId);
            if (group == null)
            {
                return HttpNotFound();
            }

            return GroupView(currentUser, group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> RenameGroup(string owner, string groupId, RenameStagingGroupViewModel model)
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(groupId))
            {
                return HttpNotFound();
            }

            var currentUser = GetCurrentUser();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledOwner(currentUser, owner);
            if (stagingOwner == null)
            {
                return HttpNotFound();
            }

            var group = _packageStagingManagementService.FindStagingGroup(stagingOwner, groupId);
            if (group == null)
            {
                return HttpNotFound();
            }

            if (model == null)
            {
                ModelState.AddModelError(nameof(RenameStagingGroupViewModel.Name), "The Display name field is required.");
            }

            if (!ModelState.IsValid)
            {
                return GroupView(currentUser, group);
            }

            group = await _packageStagingManagementService.RenameStagingGroupAsync(stagingOwner, groupId, model.Name);
            if (group == null)
            {
                return HttpNotFound();
            }

            return Redirect(Url.ManageStagingGroup(group.Owner.Username, group.Id));
        }

        [HttpGet]
        public virtual ActionResult DeleteGroup(string owner, string groupId)
        {
            var result = FindStagingGroupSummary(owner, groupId);
            if (result == null)
            {
                return HttpNotFound();
            }

            return View(CreateDeleteGroupViewModel(result));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> DeleteGroup(string owner, string groupId, DeleteStagingGroupViewModel model)
        {
            var summary = FindStagingGroupSummary(owner, groupId);
            if (summary == null)
            {
                return HttpNotFound();
            }

            var result = await _packageStagingManagementService.DeleteStagingGroupAsync(summary.Group.Owner, summary.Group);
            switch (result.Type)
            {
                case StagingGroupDeletionResultType.Deleted:
                    return Redirect(Url.ManageMyPackages());
                case StagingGroupDeletionResultType.Conflict:
                    ModelState.AddModelError(string.Empty, "The staging group cannot be deleted while package promotion is active.");
                    var viewModel = CreateDeleteGroupViewModel(summary);
                    viewModel.PackageCount = result.AffectedPackageCount;
                    return View(viewModel);
                default:
                    throw new InvalidOperationException($"Unknown staging group deletion result '{result.Type}'.");
            }
        }

        private StagingGroupSummary FindStagingGroupSummary(string owner, string groupId)
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(groupId))
            {
                return null;
            }

            var stagingOwner = _packageStagingAuthorizationService.GetEnabledOwner(GetCurrentUser(), owner);
            if (stagingOwner == null)
            {
                return null;
            }

            return _packageStagingManagementService
                .GetStagingGroupSummaries(stagingOwner)
                .SingleOrDefault(summary => string.Equals(summary.Group.Id, groupId, StringComparison.OrdinalIgnoreCase));
        }

        private static DeleteStagingGroupViewModel CreateDeleteGroupViewModel(StagingGroupSummary summary)
        {
            return new DeleteStagingGroupViewModel
            {
                Owner = summary.Group.Owner.Username,
                Id = summary.Group.Id,
                Name = summary.Group.Name,
                PackageCount = summary.Packages.Count,
            };
        }

        private ActionResult GroupView(User currentUser, StagingGroup group)
        {
            var stagedPackages = _packageStagingManagementService
                .GetStagedPackages(currentUser)
                .Where(stagedPackage =>
                    stagedPackage.StagedPackageIdentity.OwnerKey == group.OwnerKey &&
                    stagedPackage.StagedPackageIdentity.StagingGroupKey == group.Key)
                .ToList();
            var stagingGroups = _packageStagingManagementService
                .GetStagingGroups(currentUser)
                .Where(candidate => candidate.OwnerKey == group.OwnerKey)
                .ToList();

            return View(CreateGroupViewModel(group.Owner.Username, group.Id, group.Name, null, stagedPackages, stagingGroups));
        }

        [HttpGet]
        public virtual ActionResult Ungrouped(string owner)
        {
            if (string.IsNullOrWhiteSpace(owner))
            {
                return HttpNotFound();
            }

            var stagedPackages = _packageStagingManagementService
                .GetStagedPackages(GetCurrentUser())
                .Where(package => package.StagedPackageIdentity.StagingGroupKey == null)
                .Where(package => string.Equals(package.StagedPackageIdentity.Owner.Username, owner, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (stagedPackages.Count == 0)
            {
                return HttpNotFound();
            }

            var canonicalOwner = stagedPackages[0].StagedPackageIdentity.Owner.Username;
            var stagingGroups = _packageStagingManagementService
                .GetStagingGroups(GetCurrentUser())
                .Where(group => string.Equals(group.Owner.Username, canonicalOwner, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var model = CreateGroupViewModel(canonicalOwner, id: null, "Ungrouped", "Staged packages not in any group", stagedPackages, stagingGroups);

            return View("Group", model);
        }

        [HttpGet]
        public virtual ActionResult MovePackage(string owner, string id, string version)
        {
            ValidatePackageIdentity(id, version);
            if (string.IsNullOrWhiteSpace(owner))
            {
                return HttpNotFound();
            }

            var currentUser = GetCurrentUser();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledOwner(currentUser, owner);
            var stagedPackage = _packageStagingManagementService.FindCurrentStagedPackage(id, version);
            if (stagingOwner == null
                || stagedPackage == null
                || stagedPackage.StagedPackageIdentity.OwnerKey != stagingOwner.Key
                || !_packageStagingAuthorizationService.CanManage(currentUser, stagedPackage))
            {
                return HttpNotFound();
            }

            return View(CreateMovePackageViewModel(currentUser, stagedPackage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> MovePackage(string owner, string id, string version, MoveStagedPackageViewModel model)
        {
            ValidatePackageIdentity(id, version);
            if (string.IsNullOrWhiteSpace(owner))
            {
                return HttpNotFound();
            }

            var currentUser = GetCurrentUser();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledOwner(currentUser, owner);
            var stagedPackage = _packageStagingManagementService.FindCurrentStagedPackage(id, version);
            if (stagingOwner == null
                || stagedPackage == null
                || stagedPackage.StagedPackageIdentity.OwnerKey != stagingOwner.Key
                || !_packageStagingAuthorizationService.CanManage(currentUser, stagedPackage))
            {
                return HttpNotFound();
            }

            if (model == null)
            {
                ModelState.AddModelError(nameof(MoveStagedPackageViewModel.GroupId), "Select an available staging group.");
            }

            var group = string.IsNullOrWhiteSpace(model?.GroupId) ? null : _packageStagingManagementService.FindStagingGroup(stagingOwner, model.GroupId);
            if (!string.IsNullOrWhiteSpace(model?.GroupId) && group == null)
            {
                ModelState.AddModelError(nameof(model.GroupId), "Select an available staging group.");
            }

            if (!ModelState.IsValid)
            {
                var viewModel = CreateMovePackageViewModel(currentUser, stagedPackage);
                viewModel.GroupId = model?.GroupId;
                return View(viewModel);
            }

            StagingGroupMembershipResult result;
            if (group == null)
            {
                result = await _packageStagingManagementService.RemovePackageFromStagingGroupAsync(stagingOwner, stagedPackage);
            }
            else
            {
                result = await _packageStagingManagementService.AddPackageToStagingGroupAsync(stagingOwner, group, stagedPackage);
            }

            switch (result)
            {
                case StagingGroupMembershipResult.Updated:
                case StagingGroupMembershipResult.Unchanged:
                    return group == null
                        ? Redirect(Url.ManageUngroupedStaging(stagingOwner.Username))
                        : Redirect(Url.ManageStagingGroup(group.Owner.Username, group.Id));
                case StagingGroupMembershipResult.Conflict:
                    ModelState.AddModelError(string.Empty, "The staged package cannot be moved while promotion is active.");
                    var viewModel = CreateMovePackageViewModel(currentUser, stagedPackage);
                    viewModel.GroupId = model.GroupId;
                    return View(viewModel);
                default:
                    throw new InvalidOperationException($"Unknown staging group membership result '{result}'.");
            }
        }

        private MoveStagedPackageViewModel CreateMovePackageViewModel(User currentUser, StagedPackage stagedPackage)
        {
            var identity = stagedPackage.StagedPackageIdentity;
            var groups = _packageStagingManagementService
                .GetStagingGroups(currentUser)
                .Where(group => group.OwnerKey == identity.OwnerKey && group.Key != identity.StagingGroupKey)
                .OrderBy(group => group.Name)
                .ThenBy(group => group.Id)
                .Select(group => new StagingGroupAssignmentViewModel
                {
                    Id = group.Id,
                    Name = group.Name,
                })
                .ToList();
            if (identity.StagingGroupKey.HasValue)
            {
                groups.Insert(0, new StagingGroupAssignmentViewModel
                {
                    Id = null,
                    Name = "Ungrouped",
                });
            }

            return new MoveStagedPackageViewModel
            {
                Owner = identity.Owner.Username,
                Id = identity.Package.PackageRegistration.Id,
                Version = identity.Package.NormalizedVersion,
                Groups = groups,
                GroupId = groups.FirstOrDefault()?.Id,
            };
        }

        private IReadOnlyList<string> GetStagingOwnerNames()
        {
            return _packageStagingAuthorizationService
                .GetEnabledOwners(GetCurrentUser())
                .Select(owner => owner.Username)
                .ToList();
        }

        private StagingGroupDetailViewModel CreateGroupViewModel(
            string owner,
            string id,
            string name,
            string description,
            IReadOnlyCollection<StagedPackage> stagedPackages,
            IReadOnlyCollection<StagingGroup> stagingGroups)
        {
            var orderedStagedPackages = stagedPackages
                .OrderBy(stagedPackage => stagedPackage.StagedPackageIdentity.Package.PackageRegistration.Id)
                .ThenBy(stagedPackage => stagedPackage.StagedPackageIdentity.Package.NormalizedVersion)
                .ThenBy(stagedPackage => stagedPackage.Key)
                .ToList();
            var failedPackageKeys = orderedStagedPackages
                .Where(stagedPackage => stagedPackage.Status == StagedPackageStatus.FailedValidation)
                .Select(stagedPackage => stagedPackage.Key)
                .ToList();
            var validationIssues = failedPackageKeys.Count == 0
                ? new Dictionary<int, IReadOnlyList<ValidationIssue>>()
                : _validationService.GetStagedPackageValidationIssues(failedPackageKeys);
            var packageViewModels = orderedStagedPackages
                .Select(stagedPackage =>
                {
                    validationIssues.TryGetValue(stagedPackage.Key, out var issues);
                    return new PackageStagingViewModel
                    {
                        Id = stagedPackage.StagedPackageIdentity.Package.PackageRegistration.Id,
                        Version = stagedPackage.StagedPackageIdentity.Package.NormalizedVersion,
                        Owner = stagedPackage.StagedPackageIdentity.Owner.Username,
                        Status = stagedPackage.Status.ToString(),
                        StatusClass = $"staging-status-{stagedPackage.Status.ToString().ToLowerInvariant()}",
                        UploadedDate = stagedPackage.UploadedDate,
                        ValidationIssues = issues ?? [],
                        Listed = stagedPackage.StagedPackageIdentity.Package.Listed,
                        CanManage = true,
                        CanPromote = stagedPackage.Status == StagedPackageStatus.Ready && !stagedPackage.StagedPackageIdentity.StagingGroupKey.HasValue,
                        MoveUrl = stagingGroups.Any(group => group.Key != stagedPackage.StagedPackageIdentity.StagingGroupKey)
                            ? Url.MoveStagedPackage(stagedPackage.StagedPackageIdentity.Owner.Username, stagedPackage.StagedPackageIdentity.Package.PackageRegistration.Id, stagedPackage.StagedPackageIdentity.Package.NormalizedVersion)
                            : null,
                    };
                })
                .ToList();

            return new StagingGroupDetailViewModel
            {
                Owner = owner,
                Id = id,
                Name = name,
                Description = description,
                IsUngrouped = id == null,
                PackageCount = packageViewModels.Count,
                ReadyCount = orderedStagedPackages.Count(stagedPackage => stagedPackage.Status == StagedPackageStatus.Ready),
                ValidatingCount = orderedStagedPackages.Count(stagedPackage => stagedPackage.Status == StagedPackageStatus.Validating),
                FailedCount = orderedStagedPackages.Count(stagedPackage => stagedPackage.Status == StagedPackageStatus.FailedValidation),
                Packages = packageViewModels,
            };
        }

        [HttpGet]
        public virtual async Task<ActionResult> DownloadPackage(string id, string version)
        {
            ValidatePackageIdentity(id, version);

            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return HttpNotFound();
            }

            var content = await _packageStagingManagementService.OpenPackageContentAsync(stagedPackage);
            if (content == null)
            {
                return HttpNotFound();
            }

            return File(content, CoreConstants.PackageContentType, $"{id}.{version}{CoreConstants.NuGetPackageFileExtension}");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ReplacePackage(string id, string version, HttpPostedFileBase packageFile)
        {
            ValidatePackageIdentity(id, version);

            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return HttpNotFound();
            }

            if (packageFile == null || packageFile.ContentLength == 0)
            {
                TempData["ErrorMessage"] = "Select a package file.";
                return Redirect(Url.ManageMyPackages());
            }

            var result = await _packageStagingUploadService.ReplacePackageAsync(
                GetCurrentUser(),
                HttpContext,
                stagedPackage,
                packageFile.InputStream);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }

            return Redirect(Url.ManageMyPackages());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> UpdateListed(string id, string version, bool listed)
        {
            ValidatePackageIdentity(id, version);

            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return HttpNotFound();
            }

            await _packageStagingManagementService.UpdateListedAsync(stagedPackage, listed);
            return new HttpStatusCodeResult(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> DeletePackage(string id, string version)
        {
            ValidatePackageIdentity(id, version);

            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return HttpNotFound();
            }

            await _packageStagingManagementService.DeletePackageAsync(stagedPackage);
            return Redirect(Url.ManageMyPackages());
        }

        /// <summary>
        /// Begins asynchronous publication of a ready staged package.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> PromotePackage(string id, string version)
        {
            ValidatePackageIdentity(id, version);

            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return HttpNotFound();
            }

            var result = await _packageStagingPromotionService.PromotePackageAsync(GetCurrentUser(), stagedPackage);
            switch (result)
            {
                case PackageStagingPromotionResult.Accepted:
                    return Redirect(Url.ManageMyPackages());
                case PackageStagingPromotionResult.Unauthorized:
                    return HttpNotFound();
                case PackageStagingPromotionResult.NotReady:
                    TempData["ErrorMessage"] = "The staged package is not ready for promotion.";
                    return Redirect(Url.ManageMyPackages());
                case PackageStagingPromotionResult.Grouped:
                    TempData["ErrorMessage"] = "Promote this package with its staging group.";
                    return Redirect(Url.ManageMyPackages());
                case PackageStagingPromotionResult.Conflict:
                    TempData["ErrorMessage"] = "The staged package changed before promotion could begin. Try again.";
                    return Redirect(Url.ManageMyPackages());
                default:
                    throw new InvalidOperationException($"Unknown package promotion result '{result}'.");
            }
        }

        private static void ValidatePackageIdentity(string id, string version)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentNullException(nameof(version));
            }
        }

        private StagedPackage FindAuthorizedStagedPackage(string id, string version)
        {
            var stagedPackage = _packageStagingManagementService.FindCurrentStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return null;
            }

            var currentUser = GetCurrentUser();
            if (!_packageStagingAuthorizationService.CanManage(currentUser, stagedPackage))
            {
                return null;
            }

            return stagedPackage;
        }
    }
}
