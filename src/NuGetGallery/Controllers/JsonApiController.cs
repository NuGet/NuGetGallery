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
        private readonly IEntityRepository<PackageOwnerRequest> _packageOwnerRequestRepository;
        private readonly IPackageService _packageService;
        private readonly IUserService _userService;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ISecurityPolicyService _policyService;

        public JsonApiController(
            IPackageService packageService,
            IUserService userService,
            IEntityRepository<PackageOwnerRequest> packageOwnerRequestRepository,
            IMessageService messageService,
            IAppConfiguration appConfiguration,
            ISecurityPolicyService policyService)
        {
            _packageService = packageService;
            _userService = userService;
            _packageOwnerRequestRepository = packageOwnerRequestRepository;
            _messageService = messageService;
            _appConfiguration = appConfiguration;
            _policyService = policyService;
        }

        [HttpGet]
        public virtual ActionResult GetPackageOwners(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return Json(new { message = Strings.AddOwner_PackageNotFound });
            }

            if (!package.IsOwner(HttpContext.User))
            {
                return new HttpUnauthorizedResult();
            }

            var owners = from u in package.PackageRegistration.Owners
                         select new
                             {
                                 Name = u.Username,
                                 EmailAddress = u.EmailAddress,
                                 Current = u.Username == HttpContext.User.Identity.Name,
                                 Pending = false
                             };

            var pending = from u in _packageOwnerRequestRepository.GetAll()
                          where u.PackageRegistrationKey == package.PackageRegistration.Key
                          select new
                              {
                                  Name = u.NewOwner.Username,
                                  EmailAddress = u.NewOwner.EmailAddress,
                                  Current = false,
                                  Pending = true
                              };

            var result = owners.Union(pending).Select(o => new
            {
                name = o.Name,
                profileUrl = Url.User(o.Name),
                imageUrl = GravatarHelper.Url(o.EmailAddress, size: 32),
                current = o.Current,
                pending = o.Pending,
            });

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public virtual ActionResult GetAddPackageOwnerConfirmation(string id, string username)
        {
            ManagePackageOwnerModel model;
            if (TryGetManagePackageOwnerModel(id, username, out model))
            {
                return Json(new {
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
        public async Task<JsonResult> AddPackageOwner(string id, string username, string message)
        {
            ManagePackageOwnerModel model;
            if (TryGetManagePackageOwnerModel(id, username, out model))
            {
                var encodedMessage = HttpUtility.HtmlEncode(message);
                var ownerRequest = await _packageService.CreatePackageOwnerRequestAsync(
                    model.Package, model.CurrentUser, model.User);
                var confirmationUrl = Url.ConfirmationUrl(
                    "ConfirmOwner",
                    "Packages",
                    model.User.Username,
                    ownerRequest.ConfirmationCode,
                    new { id = model.Package.Id });
                var packageUrl = Url.Package(model.Package.Id, null, scheme: "http");
                var policyMessage = GetNoticeOfPoliciesRequiredMessage(model.Package, model.User, model.CurrentUser);

                _messageService.SendPackageOwnerRequest(model.CurrentUser, model.User, model.Package, packageUrl,
                    confirmationUrl, encodedMessage, policyMessage);

                return Json(new
                {
                    success = true,
                    name = model.User.Username,
                    profileUrl = Url.User(model.User.Username),
                    imageUrl = GravatarHelper.Url(model.User.EmailAddress, size: 32),
                    pending = true
                });
            }
            else
            {
                return Json(new { success = false, message = model.Error }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<JsonResult> RemovePackageOwner(string id, string username)
        {
            ManagePackageOwnerModel model;
            if (TryGetManagePackageOwnerModel(id, username, out model))
            {
                await _packageService.RemovePackageOwnerAsync(model.Package, model.User);

                _messageService.SendPackageOwnerRemovedNotice(model.CurrentUser, model.User, model.Package);

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
            return _packageOwnerRequestRepository.GetAll()
                .Where(po => po.PackageRegistrationKey == package.Key)
                .Select(po => po.NewOwner)
                .Where(RequireSecurePushForCoOwnersPolicy.IsSubscribed)
                .Select(po => po.Username);
        }

        private string GetSecurePushPolicyDescriptions()
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.SecurePushPolicyDescriptionsHtml,
                SecurePushSubscription.MinClientVersion, SecurePushSubscription.PushKeysExpirationInDays);
        }

        private bool TryGetManagePackageOwnerModel(string id, string username, out ManagePackageOwnerModel model)
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
            if (!package.IsOwner(HttpContext.User))
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

            var currentUser = _userService.FindByUsername(HttpContext.User.Identity.Name);
            if (currentUser == null)
            {
                model = new ManagePackageOwnerModel(Strings.AddOwner_CurrentUserNotFound);
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