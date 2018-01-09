// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Configuration;
using NuGetGallery.Helpers;
using NuGetGallery.Security;

namespace NuGetGallery
{
    [Authorize]
    public partial class JsonApiController
        : AppController
    {
        private readonly IMessageService _messageService;
        private readonly IPackageService _packageService;
        private readonly IUserService _userService;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ISecurityPolicyService _policyService;
        private readonly IPackageOwnershipManagementService _packageOwnershipManagementService;

        public JsonApiController(
            IPackageService packageService,
            IUserService userService,
            IMessageService messageService,
            IAppConfiguration appConfiguration,
            ISecurityPolicyService policyService,
            IPackageOwnershipManagementService packageOwnershipManagementService)
        {
            _packageService = packageService;
            _userService = userService;
            _messageService = messageService;
            _appConfiguration = appConfiguration;
            _policyService = policyService;
            _packageOwnershipManagementService = packageOwnershipManagementService;
        }

        [HttpGet]
        public virtual ActionResult GetPackageOwners(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return Json(new { message = Strings.AddOwner_PackageNotFound });
            }

            var currentUser = GetCurrentUser();
            if (ActionsRequiringPermissions.ManagePackageOwnership.CheckPermissionsOnBehalfOfAnyAccount(currentUser, package) != PermissionsCheckResult.Allowed)
            {
                return new HttpUnauthorizedResult();
            }

            var packageRegistration = package.PackageRegistration;
            var packageRegistrationOwners = package.PackageRegistration.Owners;
            var allMatchingNamespaceOwners = package
                .PackageRegistration
                .ReservedNamespaces
                .SelectMany(rn => rn.Owners)
                .Distinct();

            var packageAndReservedNamespaceOwners = packageRegistrationOwners.Intersect(allMatchingNamespaceOwners);
            var packageOwnersOnly = packageRegistrationOwners.Except(packageAndReservedNamespaceOwners);

            var owners =
                packageAndReservedNamespaceOwners
                .Select(u => new PackageOwnersResultViewModel(
                    u, 
                    currentUser,
                    packageRegistration,
                    Url,
                    isPending: false, 
                    isNamespaceOwner: true));

            var packageOwnersOnlyResultViewModel =
                packageOwnersOnly
                .Select(u => new PackageOwnersResultViewModel(
                    u,
                    currentUser,
                    packageRegistration,
                    Url,
                    isPending: false,
                    isNamespaceOwner: false));

            owners = owners.Union(packageOwnersOnlyResultViewModel);

            var pending =
                _packageOwnershipManagementService.GetPackageOwnershipRequests(package: package.PackageRegistration)
                .Select(r => new PackageOwnersResultViewModel(
                    r.NewOwner,
                    currentUser,
                    packageRegistration,
                    Url,
                    isPending: true,
                    isNamespaceOwner: false));

            var result = owners.Union(pending);

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public virtual ActionResult GetAddPackageOwnerConfirmation(string id, string username)
        {
            ManagePackageOwnerModel model;
            if (TryGetManagePackageOwnerModel(id, username, isAddOwner: true, model: out model))
            {
                return Json(new
                {
                    success = true,
                    confirmation = string.Format(CultureInfo.CurrentCulture, Strings.AddOwnerConfirmation, username),
                    policyMessage = GetNoticeOfPoliciesRequiredConfirmation(model.Package, model.User, model.CurrentUser)
                },
                JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(new { success = false, message = model.Error }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AddPackageOwner(string id, string username, string message)
        {
            ManagePackageOwnerModel model;
            if (TryGetManagePackageOwnerModel(id, username, isAddOwner: true, model: out model))
            {
                var encodedMessage = HttpUtility.HtmlEncode(message);

                var ownerRequest = await _packageOwnershipManagementService.AddPackageOwnershipRequestAsync(
                    model.Package, model.CurrentUser, model.User);

                var confirmationUrl = Url.ConfirmPendingOwnershipRequest(
                    model.Package.Id,
                    model.User.Username,
                    ownerRequest.ConfirmationCode,
                    relativeUrl: false);

                var rejectionUrl = Url.RejectPendingOwnershipRequest(
                    model.Package.Id,
                    model.User.Username,
                    ownerRequest.ConfirmationCode,
                    relativeUrl: false);

                var packageUrl = Url.Package(model.Package.Id, version: null, relativeUrl: false);
                var policyMessage = GetNoticeOfPoliciesRequiredMessage(model.Package, model.User, model.CurrentUser);

                _messageService.SendPackageOwnerRequest(model.CurrentUser, model.User, model.Package, packageUrl,
                    confirmationUrl, rejectionUrl, encodedMessage, policyMessage);

                return Json(new
                {
                    success = true,
                    model = new PackageOwnersResultViewModel(
                        model.User,
                        model.CurrentUser,
                        model.Package,
                        Url,
                        isPending: true,
                        isNamespaceOwner: false)
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
                        throw new InvalidOperationException("You can't remove the only owner from a package.");
                    }
                    await _packageOwnershipManagementService.RemovePackageOwnerAsync(model.Package, model.CurrentUser, model.User, commitAsTransaction:true);
                    _messageService.SendPackageOwnerRemovedNotice(model.CurrentUser, model.User, model.Package);
                }
                else
                {
                    await _packageOwnershipManagementService.DeletePackageOwnershipRequestAsync(model.Package, model.User);
                    _messageService.SendPackageOwnerRequestCancellationNotice(model.CurrentUser, model.User, model.Package);
                }

                return Json(new { success = true });
            }
            else
            {
                return Json(new { success = false, message = model.Error }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// UI confirmation message for adding owner from ManageOwners.cshtml
        /// </summary>
        private string GetNoticeOfPoliciesRequiredConfirmation(PackageRegistration package, User user, User currentUser)
        {
            if (IsFirstPropagatingOwner(package, user))
            {
                return string.Format(CultureInfo.CurrentCulture,
                    Strings.AddOwnerConfirmation_SecurePushRequiredByNewOwner,
                    user.Username, GetSecurePushPolicyDescriptions(), _appConfiguration.GalleryOwner.Address);
            }
            else if (!_policyService.IsSubscribed(user, SecurePushSubscription.Name))
            {
                IEnumerable<string> propagating = null;
                if ((propagating = GetPropagatingOwners(package)).Any())
                {
                    var propagators = string.Join(", ", propagating);
                    return string.Format(CultureInfo.CurrentCulture,
                        Strings.AddOwnerConfirmation_SecurePushRequiredByOwner,
                        propagators, user.Username, GetSecurePushPolicyDescriptions(), _appConfiguration.GalleryOwner.Address);
                }
                else if ((propagating = GetPendingPropagatingOwners(package)).Any())
                {
                    var propagators = string.Join(", ", propagating);
                    return string.Format(CultureInfo.CurrentCulture,
                        Strings.AddOwnerConfirmation_SecurePushRequiredByPendingOwner,
                        propagators, user.Username, GetSecurePushPolicyDescriptions(), _appConfiguration.GalleryOwner.Address);
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Policy message for the package owner request notification.
        /// </summary>
        private string GetNoticeOfPoliciesRequiredMessage(PackageRegistration package, User user, User currentUser)
        {
            IEnumerable<string> propagating = null;

            if (IsFirstPropagatingOwner(package, user))
            {
                return string.Format(CultureInfo.CurrentCulture,
                    Strings.AddOwnerRequest_SecurePushRequiredByNewOwner,
                    _appConfiguration.GalleryOwner.Address, GetSecurePushPolicyDescriptions());
            }
            else if (!_policyService.IsSubscribed(user, SecurePushSubscription.Name))
            {
                if ((propagating = GetPropagatingOwners(package)).Any())
                {
                    var propagators = string.Join(", ", propagating);
                    return string.Format(CultureInfo.CurrentCulture,
                        Strings.AddOwnerRequest_SecurePushRequiredByOwner,
                        propagators, _appConfiguration.GalleryOwner.Address, GetSecurePushPolicyDescriptions());
                }
                else if ((propagating = GetPendingPropagatingOwners(package)).Any())
                {
                    var propagators = string.Join(", ", propagating);
                    return string.Format(CultureInfo.CurrentCulture,
                        Strings.AddOwnerRequest_SecurePushRequiredByPendingOwner,
                        propagators, _appConfiguration.GalleryOwner.Address, GetSecurePushPolicyDescriptions());
                }
            }

            return string.Empty;
        }

        private bool IsFirstPropagatingOwner(PackageRegistration package, User user)
        {
            return RequireSecurePushForCoOwnersPolicy.IsSubscribed(user) &&
                !package.Owners.Any(RequireSecurePushForCoOwnersPolicy.IsSubscribed);
        }

        private IEnumerable<string> GetPropagatingOwners(PackageRegistration package)
        {
            return package.Owners.Where(RequireSecurePushForCoOwnersPolicy.IsSubscribed).Select(o => o.Username);
        }

        private IEnumerable<string> GetPendingPropagatingOwners(PackageRegistration package)
        {
            return _packageOwnershipManagementService.GetPackageOwnershipRequests(package: package)
                .Select(po => po.NewOwner)
                .Where(RequireSecurePushForCoOwnersPolicy.IsSubscribed)
                .Select(po => po.Username);
        }

        private string GetSecurePushPolicyDescriptions()
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.SecurePushPolicyDescriptionsHtml,
                SecurePushSubscription.MinProtocolVersion, SecurePushSubscription.PushKeysExpirationInDays);
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
            }

            public PackageRegistration Package { get; }
            public User User { get; }
            public User CurrentUser { get; }
            public string Error { get; }
        }
    }
}