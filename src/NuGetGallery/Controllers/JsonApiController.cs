// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
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

        public JsonApiController(
            IPackageService packageService,
            IUserService userService,
            IMessageService messageService,
            IAppConfiguration appConfiguration,
            IPackageOwnershipManagementService packageOwnershipManagementService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            _packageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AddPackageOwner(string id, string username, string message)
        {
            if (Regex.IsMatch(username, GalleryConstants.EmailValidationRegex, RegexOptions.None, GalleryConstants.EmailValidationRegexTimeout))
            {
                return Json(new { success = false, message = Strings.AddOwner_NameIsEmail }, JsonRequestBehavior.AllowGet);
            }

            if (TryGetManagePackageOwnerModel(id, username, isAddOwner: true, model: out var model))
            {
                var packageUrl = Url.Package(model.Package.Id, version: null, relativeUrl: false);

                if (model.CurrentUserCanAcceptOnBehalfOfUser)
                {
                    await _packageOwnershipManagementService.AddPackageOwnerAsync(model.Package, model.User);

                    foreach (var owner in model.Package.Owners)
                    {
                        var emailMessage = new PackageOwnerAddedMessage(_appConfiguration, owner, model.User, model.Package, packageUrl);
                        await _messageService.SendMessageAsync(emailMessage);
                    }
                }
                else
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

                    var cancellationUrl = Url.CancelPendingOwnershipRequest(
                        model.Package.Id,
                        model.CurrentUser.Username,
                        model.User.Username,
                        relativeUrl: false);

                    var packageOwnershipRequestMessage = new PackageOwnershipRequestMessage(
                        _appConfiguration,
                        model.CurrentUser,
                        model.User,
                        model.Package,
                        packageUrl,
                        confirmationUrl,
                        rejectionUrl,
                        encodedMessage,
                        string.Empty);
                    await _messageService.SendMessageAsync(packageOwnershipRequestMessage);

                    foreach (var owner in model.Package.Owners)
                    {
                        var emailMessage = new PackageOwnershipRequestInitiatedMessage(_appConfiguration, model.CurrentUser, owner, model.User, model.Package, cancellationUrl);

                        await _messageService.SendMessageAsync(emailMessage);
                    }
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
                        return Json(new { success = false, message = "You can't remove the only owner from a package." }, JsonRequestBehavior.AllowGet);
                    }

                    await _packageOwnershipManagementService.RemovePackageOwnerAsync(model.Package, model.CurrentUser, model.User, commitAsTransaction: true);

                    var emailMessage = new PackageOwnerRemovedMessage(_appConfiguration, model.CurrentUser, model.User, model.Package);
                    await _messageService.SendMessageAsync(emailMessage);
                }
                else
                {
                    await _packageOwnershipManagementService.DeletePackageOwnershipRequestAsync(model.Package, model.User);

                    var emailMessage = new PackageOwnershipRequestCanceledMessage(_appConfiguration, model.CurrentUser, model.User, model.Package);
                    await _messageService.SendMessageAsync(emailMessage);
                }

                return Json(new { success = true });
            }
            else
            {
                return Json(new { success = false, message = model.Error }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        [ActionName("CveIds")]
        public ActionResult GetCveIds(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Search term cannot be empty.");
            }

            // We should wait for at least 4 numeric characters before suggesting.
            if (query.ToUpperInvariant().Replace(Cve.IdPrefix, string.Empty).Length < 4)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Search term must have at least 4 numeric characters. " + Strings.AutocompleteCveIds_FormatException);
            }

            var model = new CveAutocompleteDataViewModel();

            // Get CVE data.
            // Suggestions will be CVE Id's that start with characters entered by the user.
            IReadOnlyCollection<CveIdAutocompleteQueryResult> suggestions;
            try
            {
                suggestions = GetService<IAutoCompleteCveIdsQuery>().Execute(query);

                model.Items.AddRange(suggestions);
            }
            catch (FormatException formatException)
            {
                return Json(new { success = false, message = formatException.Message }, JsonRequestBehavior.AllowGet);
            }

            return Json(model, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("CweIds")]
        public ActionResult GetCweIds(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Search term cannot be empty.");
            }

            var model = new CweAutocompleteDataViewModel();

            // Get CWE data.
            // Suggestions will be CWE Id's that start with characters entered by the user,
            // or CWE Id's that have a Name containing the textual search term provided by the user.
            IReadOnlyCollection<CweIdAutocompleteQueryResult> suggestions;
            try
            {
                suggestions = GetService<IAutoCompleteCweIdsQuery>().Execute(query);

                if (suggestions != null)
                {
                    model.Items.AddRange(suggestions);
                }
            }
            catch (FormatException formatException)
            {
                return Json(new { success = false, message = formatException.Message }, JsonRequestBehavior.AllowGet);
            }

            return Json(model, JsonRequestBehavior.AllowGet);
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