// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Configuration;
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

        public JsonApiController(
            IPackageService packageService,
            IUserService userService,
            IEntityRepository<PackageOwnerRequest> packageOwnerRequestRepository,
            IMessageService messageService,
            IAppConfiguration appConfiguration)
        {
            _packageService = packageService;
            _userService = userService;
            _packageOwnerRequestRepository = packageOwnerRequestRepository;
            _messageService = messageService;
            _appConfiguration = appConfiguration;
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
                         select new OwnerModel
                             {
                                 name = u.Username,
                                 current = u.Username == HttpContext.User.Identity.Name,
                                 pending = false
                             };

            var pending = from u in _packageOwnerRequestRepository.GetAll()
                          where u.PackageRegistrationKey == package.PackageRegistration.Key
                          select new OwnerModel { name = u.NewOwner.Username, current = false, pending = true };

            return Json(owners.Union(pending), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("GetAddPackageOwnerConfirmation")]
        public async virtual Task<ActionResult> GetAddPackageOwnerConfirmationAsync(string id, string username)
        {
            var jsonResult = await ManagePackageOwnerAsync(id, username, (package, user, currentUser) =>
            {
                return Json(new { success = true, confirmation = GetAddPackageOwnerConfirmationMessage(package, user, currentUser) },
                    JsonRequestBehavior.AllowGet);
            });
            
            return jsonResult;
        }

        [HttpPost]
        public Task<JsonResult> AddPackageOwner(string id, string username, string message)
        {
            return ManagePackageOwnerAsync(id, username, async (package, user, currentUser) =>
            {
                var encodedMessage = HttpUtility.HtmlEncode(message);
                var ownerRequest = await _packageService.CreatePackageOwnerRequestAsync(package, currentUser, user);
                var confirmationUrl = Url.ConfirmationUrl(
                    "ConfirmOwner",
                    "Packages",
                    user.Username,
                    ownerRequest.ConfirmationCode,
                    new { id = package.Id });
                var packageUrl = Url.Package(package);
                var policyMessage = GetNoticeOfPoliciesRequiredMessage(package, user, currentUser);

                _messageService.SendPackageOwnerRequest(currentUser, user, package, packageUrl, confirmationUrl, encodedMessage, policyMessage);

                return Json(new { success = true, name = user.Username, pending = true });
            });
        }

        [HttpPost]
        public Task<JsonResult> RemovePackageOwner(string id, string username)
        {
            return ManagePackageOwnerAsync(id, username, async (package, user, currentUser) =>
            {
                await _packageService.RemovePackageOwnerAsync(package, user);

                _messageService.SendPackageOwnerRemovedNotice(currentUser, user, package);

                return Json(new { success = true });
            });
        }

        /// <summary>
        /// UI confirmation message for adding owner from ManageOwners.cshtml
        /// </summary>
        private string GetAddPackageOwnerConfirmationMessage(PackageRegistration package, User user, User currentUser)
        {
            var defaultMessage = string.Format(CultureInfo.CurrentCulture, Strings.AddOwnerConfirmation, user.Username);

            if (RequireSecurePushForCoOwnersPolicy.IsSubscribed(user))
            {
                return string.Format(CultureInfo.CurrentCulture,
                    Strings.AddOwnerConfirmation_SecurePushRequiredByNewOwner,
                    user.Username, Strings.SecurePushPolicyDescriptions, _appConfiguration.GalleryOwner.Address)
                    + Environment.NewLine + defaultMessage;
            }

            var propagatingOwners = package.Owners.Where(o => RequireSecurePushForCoOwnersPolicy.IsSubscribed(o)).Select(o => o.Username);
            if (propagatingOwners.Any())
            {
                var propagators = string.Join(", ", propagatingOwners);
                return string.Format(CultureInfo.CurrentCulture,
                    Strings.AddOwnerConfirmation_SecurePushRequiredByOwner,
                    propagators, user.Username, Strings.SecurePushPolicyDescriptions, _appConfiguration.GalleryOwner.Address)
                    + Environment.NewLine + defaultMessage;
            }

            var pendingPropagatingOwners = _packageOwnerRequestRepository.GetAll()
                .Where(po => po.PackageRegistrationKey == package.Key && RequireSecurePushForCoOwnersPolicy.IsSubscribed(po.NewOwner))
                .Select(po => po.NewOwner.Username);
            if (pendingPropagatingOwners.Any())
            {
                var propagators = string.Join(", ", pendingPropagatingOwners);
                return string.Format(CultureInfo.CurrentCulture,
                    Strings.AddOwnerConfirmation_SecurePushRequiredByPendingOwner,
                    propagators, user.Username, Strings.SecurePushPolicyDescriptions, _appConfiguration.GalleryOwner.Address)
                    + Environment.NewLine + defaultMessage;
            }

            return defaultMessage;
        }

        /// <summary>
        /// Policy message for the package owner request notification.
        /// </summary>
        private string GetNoticeOfPoliciesRequiredMessage(PackageRegistration package, User user, User currentUser)
        {
            if (RequireSecurePushForCoOwnersPolicy.IsSubscribed(user))
            {
                return string.Format(CultureInfo.CurrentCulture,
                    Strings.AddOwnerRequest_SecurePushRequiredByNewOwner,
                    _appConfiguration.GalleryOwner.Address, Strings.SecurePushPolicyDescriptions);
            }

            var propagatingOwners = package.Owners.Where(o => RequireSecurePushForCoOwnersPolicy.IsSubscribed(o)).Select(o => o.Username);
            if (propagatingOwners.Any())
            {
                var propagators = string.Join(", ", propagatingOwners);
                return string.Format(CultureInfo.CurrentCulture,
                    Strings.AddOwnerRequest_SecurePushRequiredByOwner,
                    propagators, _appConfiguration.GalleryOwner.Address, Strings.SecurePushPolicyDescriptions);
            }

            var pendingPropagatingOwners = _packageOwnerRequestRepository.GetAll()
                .Where(po => po.PackageRegistrationKey == package.Key && RequireSecurePushForCoOwnersPolicy.IsSubscribed(po.NewOwner))
                .Select(po => po.NewOwner.Username);
            if (pendingPropagatingOwners.Any())
            {
                var propagators = string.Join(", ", pendingPropagatingOwners);
                return string.Format(CultureInfo.CurrentCulture,
                    Strings.AddOwnerRequest_SecurePushRequiredByPendingOwner,
                    propagators, _appConfiguration.GalleryOwner.Address, Strings.SecurePushPolicyDescriptions);
            }

            return string.Empty;
        }

        private Task<JsonResult> ManagePackageOwnerAsync(string id, string username, Func<PackageRegistration, User, User, JsonResult> action)
        {
            return ManagePackageOwnerAsync(id, username, (package, user, currentUser) => Task.FromResult(action(package, user, currentUser)));
        }

        private Task<JsonResult> ManagePackageOwnerAsync(string id, string username, Func<PackageRegistration, User, User, Task<JsonResult>> actionAsync)
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
                return Task.FromResult(Json(new { success = false, message = Strings.AddOwner_PackageNotFound }));
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return Task.FromResult(Json(new { success = false, message = Strings.AddOwner_NotPackageOwner }));
            }
            var user = _userService.FindByUsername(username);
            if (user == null)
            {
                return Task.FromResult(Json(new { success = false, message = Strings.AddOwner_OwnerNotFound }));
            }
            if (!user.Confirmed)
            {
                return Task.FromResult(Json(new { success = false,
                    message = string.Format(CultureInfo.InvariantCulture, Strings.AddOwner_OwnerNotConfirmed, username) }));
            }
            var currentUser = _userService.FindByUsername(HttpContext.User.Identity.Name);
            if (currentUser == null)
            {
                return Task.FromResult(Json(new { success = false, message = Strings.AddOwner_CurrentUserNotFound }));
            }
            return actionAsync(package, user, currentUser);
        }

        public class OwnerModel
        {
            public string name { get; set; }
            public bool current { get; set; }
            public bool pending { get; set; }
        }
    }
}