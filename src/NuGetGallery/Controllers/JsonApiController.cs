﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Configuration;
using NuGetGallery.Filters;
using NuGetGallery.Infrastructure.Mail.Messages;

namespace NuGetGallery
{
    [UIAuthorize]
    public partial class JsonApiController
        : AppController
    {
        private readonly IMessageService _messageService;
        private readonly IPackageService _packageService;
        private readonly IUserService _userService;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IPackageOwnershipManagementService _packageOwnershipManagementService;
        private readonly IFeatureFlagService _features;

        public JsonApiController(
            IPackageService packageService,
            IUserService userService,
            IMessageService messageService,
            IAppConfiguration appConfiguration,
            IPackageOwnershipManagementService packageOwnershipManagementService,
            IFeatureFlagService features)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            _packageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
            _features = features ?? throw new ArgumentNullException(nameof(features));
        }

        [HttpGet]
        public virtual ActionResult GetPackageOwners(string id)
        {
            var registration = _packageService.FindPackageRegistrationById(id);
            if (registration == null)
            {
                return Json(new { message = Strings.AddOwner_PackageNotFound });
            }

            var currentUser = GetCurrentUser();
            if (ActionsRequiringPermissions.ManagePackageOwnership.CheckPermissionsOnBehalfOfAnyAccount(currentUser, registration) != PermissionsCheckResult.Allowed)
            {
                return new HttpUnauthorizedResult();
            }

            var packageRegistrationOwners = registration.Owners;
            var allMatchingNamespaceOwners = registration
                .ReservedNamespaces
                .SelectMany(rn => rn.Owners)
                .Distinct();

            var packageAndReservedNamespaceOwners = packageRegistrationOwners.Intersect(allMatchingNamespaceOwners);
            var packageOwnersOnly = packageRegistrationOwners.Except(packageAndReservedNamespaceOwners);
            var proxyGravatar = _features.IsGravatarProxyEnabled();

            var owners =
                packageAndReservedNamespaceOwners
                .Select(user => new PackageOwnersResultViewModel(
                    user,
                    currentUser,
                    registration,
                    Url,
                    isPending: false,
                    isNamespaceOwner: true,
                    avatarUrl: Url.Avatar(
                        user,
                        proxyGravatar,
                        GalleryConstants.GravatarImageSize)));

            var packageOwnersOnlyResultViewModel =
                packageOwnersOnly
                .Select(user => new PackageOwnersResultViewModel(
                    user,
                    currentUser,
                    registration,
                    Url,
                    isPending: false,
                    isNamespaceOwner: false,
                    avatarUrl: Url.Avatar(
                        user,
                        proxyGravatar,
                        GalleryConstants.GravatarImageSize)));

            owners = owners.Union(packageOwnersOnlyResultViewModel);

            var pending =
                _packageOwnershipManagementService.GetPackageOwnershipRequests(package: registration)
                .Select(r => new PackageOwnersResultViewModel(
                    r.NewOwner,
                    currentUser,
                    registration,
                    Url,
                    isPending: true,
                    isNamespaceOwner: false,
                    avatarUrl: Url.Avatar(
                        r.NewOwner,
                        proxyGravatar,
                        GalleryConstants.GravatarImageSize)));

            var result = owners.Union(pending);

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AddPackageOwner(AddPackageOwnerViewModel addOwnerData)
        {
            string id = addOwnerData.Id;
            string username = addOwnerData.Username;
            string message = addOwnerData.Message;

            if (Regex.IsMatch(username, GalleryConstants.EmailValidationRegex, RegexOptions.None, GalleryConstants.EmailValidationRegexTimeout))
            {
                return Json(new { success = false, message = Strings.AddOwner_NameIsEmail }, JsonRequestBehavior.AllowGet);
            }

            if (TryGetManagePackageOwnerModel(id, username, isAddOwner: true, model: out var model))
            {
                var packageUrl = Url.Package(model.Package.Id, version: null, relativeUrl: false);

                if (model.CurrentUserCanAcceptOnBehalfOfUser)
                {
                    await _packageOwnershipManagementService.AddPackageOwnerWithMessagesAsync(model.Package, model.User);
                }
                else
                {
                    await _packageOwnershipManagementService.AddPackageOwnershipRequestWithMessagesAsync(
                        model.Package,
                        model.CurrentUser,
                        model.User,
                        message);
                }

                return Json(new
                {
                    success = true,
                    model = new PackageOwnersResultViewModel(
                        model.User,
                        model.CurrentUser,
                        model.Package,
                        Url,
                        isPending: !model.CurrentUserCanAcceptOnBehalfOfUser,
                        isNamespaceOwner: false,
                        avatarUrl: Url.Avatar(
                            model.User,
                            _features.IsGravatarProxyEnabled(),
                            GalleryConstants.GravatarImageSize))
                });
            }
            else
            {
                return Json(new { success = false, message = model.Error }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> RemovePackageOwner(string id, string username)
        {
            ManagePackageOwnerModel model;
            if (TryGetManagePackageOwnerModel(id, username, isAddOwner: false, model: out model))
            {
                var request = _packageOwnershipManagementService.GetPackageOwnershipRequests(package: model.Package, newOwner: model.User).FirstOrDefault();

                if (request == null)
                {
                    if (model.Package.Owners.Count == 1 && model.User == model.Package.Owners.Single())
                    {
                        return Json(new { success = false, message = "You can't remove the only owner from a package." }, JsonRequestBehavior.AllowGet);
                    }

                    await _packageOwnershipManagementService.RemovePackageOwnerWithMessagesAsync(model.Package, model.CurrentUser, model.User);
                }
                else
                {
                    await _packageOwnershipManagementService.CancelPackageOwnershipRequestWithMessagesAsync(model.Package, model.CurrentUser, model.User);
                }

                return Json(new { success = true });
            }
            else
            {
                return Json(new { success = false, message = model.Error }, JsonRequestBehavior.AllowGet);
            }
        }

        private bool TryGetManagePackageOwnerModel(string id, string username, bool isAddOwner, out ManagePackageOwnerModel model)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(nameof(id));
            }
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException(nameof(username));
            }

            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                model = new ManagePackageOwnerModel(Strings.AddOwner_PackageNotFound);
                return false;
            }

            var currentUser = GetCurrentUser();
            if (currentUser == null)
            {
                model = new ManagePackageOwnerModel(Strings.AddOwner_CurrentUserNotFound);
                return false;
            }

            if (currentUser.IsLocked)
            {
                model = new ManagePackageOwnerModel(ServicesStrings.UserAccountIsLocked);
                return false;
            }

            if (ActionsRequiringPermissions.ManagePackageOwnership.CheckPermissionsOnBehalfOfAnyAccount(currentUser, package) != PermissionsCheckResult.Allowed)
            {
                model = new ManagePackageOwnerModel(Strings.AddOwner_NotPackageOwner);
                return false;
            }

            var user = _userService.FindByUsername(username);
            if (user == null)
            {
                model = new ManagePackageOwnerModel(Strings.AddOwner_OwnerNotFound);
                return false;
            }
            if (!user.Confirmed)
            {
                model = new ManagePackageOwnerModel(
                    string.Format(CultureInfo.CurrentCulture, Strings.AddOwner_OwnerNotConfirmed, username));
                return false;
            }

            if (user.IsLocked)
            {
                model = new ManagePackageOwnerModel(
                    string.Format(CultureInfo.CurrentCulture, ServicesStrings.SpecificAccountIsLocked, username));
                return false;
            }

            var isOwner =
                package.Owners.Any(o => o.MatchesUser(user)) ||
                _packageOwnershipManagementService.GetPackageOwnershipRequests(package: package, newOwner: user).Any();

            if (isAddOwner && isOwner)
            {
                model = new ManagePackageOwnerModel(
                    string.Format(CultureInfo.CurrentCulture, Strings.AddOwner_AlreadyOwner, username));
                return false;
            }

            if (!isAddOwner && !isOwner)
            {
                model = new ManagePackageOwnerModel(
                    string.Format(CultureInfo.CurrentCulture, Strings.RemoveOwner_NotOwner, username));
                return false;
            }

            model = new ManagePackageOwnerModel(package, user, currentUser);
            return true;
        }

        private class ManagePackageOwnerModel
        {
            public ManagePackageOwnerModel(string error)
            {
                Error = error;
            }

            public ManagePackageOwnerModel(PackageRegistration package, User user, User currentUser)
            {
                Package = package;
                User = user;
                CurrentUser = currentUser;
                CurrentUserCanAcceptOnBehalfOfUser =
                    ActionsRequiringPermissions.HandlePackageOwnershipRequest.CheckPermissions(currentUser, user) == PermissionsCheckResult.Allowed;
            }

            public PackageRegistration Package { get; }
            public User User { get; }
            public User CurrentUser { get; }
            public bool CurrentUserCanAcceptOnBehalfOfUser { get; }
            public string Error { get; }
        }
    }
}