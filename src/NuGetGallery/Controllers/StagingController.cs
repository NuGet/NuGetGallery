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
            if (model == null)
            {
                model = new CreateStagingGroupViewModel();
                ModelState.AddModelError(nameof(CreateStagingGroupViewModel.Owner), "The Owner field is required.");
                ModelState.AddModelError(nameof(CreateStagingGroupViewModel.Id), "The Group ID field is required.");
            }

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
                group = _packageStagingManagementService.FindStagingGroup(stagingOwner, groupId);
                if (group == null)
                {
                    return HttpNotFound();
                }

                ModelState.AddModelError(string.Empty, "The group changed or promotion started. Refresh and try again.");
                return GroupView(currentUser, group);
            }

            return Redirect(Url.ManageStagingGroup(group.Owner.Username, group.Id));
        }

        /// <summary>
        /// Begins asynchronous publication of every ready package in a staging group.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> PromoteGroup(string owner, string groupId)
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(groupId))
            {
                return HttpNotFound();
            }

            var currentUser = GetCurrentUser();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledOwner(currentUser, owner);
            var group = stagingOwner == null ? null : _packageStagingManagementService.FindStagingGroup(stagingOwner, groupId);
            if (group == null)
            {
                return HttpNotFound();
            }

            var result = await _packageStagingPromotionService.PromoteGroupAsync(currentUser, group);
            switch (result)
            {
                case StagingGroupPromotionResult.Accepted:
                    break;
                case StagingGroupPromotionResult.Unauthorized:
                    return HttpNotFound();
                case StagingGroupPromotionResult.Empty:
                    TempData["ErrorMessage"] = "The staging group has no packages to promote.";
                    break;
                case StagingGroupPromotionResult.NotReady:
                    TempData["ErrorMessage"] = "Every package in the staging group must be ready before promotion can begin.";
                    break;
                case StagingGroupPromotionResult.Conflict:
                    TempData["ErrorMessage"] = "The staging group changed before promotion could begin. Try again.";
                    break;
                default:
                    throw new InvalidOperationException($"Unknown staging group promotion result '{result}'.");
            }

            return Redirect(Url.ManageStagingGroup(group.Owner.Username, group.Id));
        }

        /// <summary>
        /// Resends unfinished work for a stalled group promotion.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ResendGroup(string owner, string groupId)
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(groupId))
            {
                return HttpNotFound();
            }

            var currentUser = GetCurrentUser();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledOwner(currentUser, owner);
            var group = stagingOwner == null ? null : _packageStagingManagementService.FindStagingGroup(stagingOwner, groupId);
            if (group == null)
            {
                return HttpNotFound();
            }

            var result = await _packageStagingPromotionService.ResendGroupAsync(currentUser, group);
            switch (result)
            {
                case StagingGroupPromotionResult.Accepted:
                    break;
                case StagingGroupPromotionResult.Unauthorized:
                    return HttpNotFound();
                case StagingGroupPromotionResult.NotReady:
                    TempData["ErrorMessage"] = "This group promotion cannot be retried yet. Refresh the page and try again later.";
                    break;
                case StagingGroupPromotionResult.Conflict:
                    TempData["ErrorMessage"] = "The group promotion changed. Refresh the page and try again.";
                    break;
                default:
                    throw new InvalidOperationException($"Unknown group promotion resend result '{result}'.");
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
        [ActionName(nameof(DeleteGroup))]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> DeleteGroupPost(string owner, string groupId)
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

            return View(nameof(Group), CreateGroupViewModel(group.Owner.Username, group.Id, group.Name, null, stagedPackages, stagingGroups, group.ActivePromotionId.HasValue, group.PromotionMessageSentDate));
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
        public virtual async Task<ActionResult> MovePackage(string owner, string id, string version, string groupId)
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

            var group = string.IsNullOrWhiteSpace(groupId) ? null : _packageStagingManagementService.FindStagingGroup(stagingOwner, groupId);
            if (!string.IsNullOrWhiteSpace(groupId) && group == null)
            {
                ModelState.AddModelError(nameof(MoveStagedPackageViewModel.GroupId), "Select an available staging group.");
            }

            if (!ModelState.IsValid)
            {
                var viewModel = CreateMovePackageViewModel(currentUser, stagedPackage);
                viewModel.GroupId = groupId;
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
                    viewModel.GroupId = groupId;
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
            IReadOnlyCollection<StagingGroup> stagingGroups,
            bool isPromotionActive = false,
            DateTime? promotionMessageSentDate = null)
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
                    var identity = stagedPackage.StagedPackageIdentity;
                    var package = identity.Package;
                    var hasMoveTarget = identity.StagingGroupKey.HasValue || stagingGroups.Any(group => group.Key != identity.StagingGroupKey);
                    var moveUrl = hasMoveTarget && !isPromotionActive ? Url.MoveStagedPackage(identity.Owner.Username, package.PackageRegistration.Id, package.NormalizedVersion) : null;

                    return new PackageStagingViewModel
                    {
                        Id = package.PackageRegistration.Id,
                        Version = package.NormalizedVersion,
                        Owner = identity.Owner.Username,
                        Status = stagedPackage.Status.ToString(),
                        StatusClass = $"staging-status-{stagedPackage.Status.ToString().ToLowerInvariant()}",
                        UploadedDate = stagedPackage.UploadedDate,
                        ValidationIssues = issues ?? [],
                        Listed = package.Listed,
                        CanManage = !isPromotionActive,
                        CanPromote = stagedPackage.Status == StagedPackageStatus.Ready && !identity.StagingGroupKey.HasValue,
                        CanResend = !identity.StagingGroupKey.HasValue
                            && stagedPackage.Status == StagedPackageStatus.Promoting
                            && stagedPackage.ActivePromotionId.HasValue
                            && StagingPromotionResendPolicy.IsDue(stagedPackage.PromotionMessageSentDate),
                        MoveUrl = moveUrl,
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
                IsPromotionActive = isPromotionActive,
                CanPromote = id != null
                    && !isPromotionActive
                    && orderedStagedPackages.Count > 0
                    && orderedStagedPackages.All(stagedPackage => stagedPackage.Status == StagedPackageStatus.Ready),
                CanResend = id != null && isPromotionActive && StagingPromotionResendPolicy.IsDue(promotionMessageSentDate),
                PackageCount = packageViewModels.Count,
                ReadyCount = orderedStagedPackages.Count(stagedPackage => stagedPackage.Status == StagedPackageStatus.Ready),
                ValidatingCount = orderedStagedPackages.Count(stagedPackage => stagedPackage.Status == StagedPackageStatus.Validating),
                PromotingCount = orderedStagedPackages.Count(stagedPackage => stagedPackage.Status == StagedPackageStatus.Promoting),
                FailedCount = orderedStagedPackages.Count(stagedPackage => stagedPackage.Status == StagedPackageStatus.FailedValidation || stagedPackage.Status == StagedPackageStatus.PromotionFailed),
                PromotionFailedCount = orderedStagedPackages.Count(stagedPackage => stagedPackage.Status == StagedPackageStatus.PromotionFailed),
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

            var updated = await _packageStagingManagementService.UpdateListedAsync(stagedPackage, listed);
            return new HttpStatusCodeResult(updated ? HttpStatusCode.NoContent : HttpStatusCode.Conflict);
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

            if (!await _packageStagingManagementService.DeletePackageAsync(stagedPackage))
            {
                TempData["ErrorMessage"] = "The staged package cannot be deleted while package promotion is active.";
            }

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

        /// <summary>
        /// Resends a stalled individual package promotion.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> ResendPackage(string id, string version)
        {
            ValidatePackageIdentity(id, version);

            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return HttpNotFound();
            }

            var result = await _packageStagingPromotionService.ResendPackageAsync(GetCurrentUser(), stagedPackage);
            switch (result)
            {
                case PackageStagingPromotionResult.Accepted:
                    break;
                case PackageStagingPromotionResult.Unauthorized:
                    return HttpNotFound();
                case PackageStagingPromotionResult.NotReady:
                    TempData["ErrorMessage"] = "This package promotion cannot be retried yet. Refresh the page and try again later.";
                    break;
                case PackageStagingPromotionResult.Grouped:
                    TempData["ErrorMessage"] = "Retry this package's promotion with its staging group.";
                    break;
                case PackageStagingPromotionResult.Conflict:
                    TempData["ErrorMessage"] = "The package promotion changed. Refresh the page and try again.";
                    break;
                default:
                    throw new InvalidOperationException($"Unknown package promotion resend result '{result}'.");
            }

            return Redirect(Url.ManageUngroupedStaging(stagedPackage.StagedPackageIdentity.Owner.Username));
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
