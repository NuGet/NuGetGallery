// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
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

        public JsonApiController(
            IPackageService packageService,
            IUserService userService,
            IEntityRepository<PackageOwnerRequest> packageOwnerRequestRepository,
            IMessageService messageService)
        {
            _packageService = packageService;
            _userService = userService;
            _packageOwnerRequestRepository = packageOwnerRequestRepository;
            _messageService = messageService;
        }

        [HttpGet]
        public virtual ActionResult GetPackageOwners(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return Json(new { message = "Package not found" });
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

        /// <summary>
        /// UI confirmation message for adding owner from ManageOwners.cshtml
        /// </summary>
        private string GetAddPackageOwnerConfirmationMessage(PackageRegistration package, User user, User currentUser)
        {
            var defaultMessage = $"Please confirm if you want to proceed adding '{user.Username}' as a co-owner of this package.";

            if (RequireSecurePushForCoOwnersPolicy.IsSubscribed(user))
            {
                return SecurePushMessages.ConfirmationOfPoliciesRequiredByNewPendingCoOwner(user)
                    + Environment.NewLine + defaultMessage;
            }

            var propagatingOwners = package.Owners.Where(o => RequireSecurePushForCoOwnersPolicy.IsSubscribed(o)).Select(o => o.Username);
            if (propagatingOwners.Any())
            {
                return SecurePushMessages.ConfirmationOfPoliciesRequiredByCoOwners(propagatingOwners, user)
                    + Environment.NewLine + defaultMessage;
            }

            var pendingPropagatingOwners = _packageOwnerRequestRepository.GetAll()
                .Where(po => po.PackageRegistrationKey == package.Key && RequireSecurePushForCoOwnersPolicy.IsSubscribed(po.NewOwner))
                .Select(po => po.NewOwner.Username);
            if (pendingPropagatingOwners.Any())
            {
                return SecurePushMessages.ConfirmationOfPoliciesRequiredByPendingCoOwners(pendingPropagatingOwners, user)
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
                return SecurePushMessages.NoticeOfPoliciesRequiredByNewPendingCoOwner(user);
            }

            var propagatingOwners = package.Owners.Where(o => RequireSecurePushForCoOwnersPolicy.IsSubscribed(o)).Select(o => o.Username);
            if (propagatingOwners.Any())
            {
                return SecurePushMessages.NoticeOfPoliciesRequiredByCoOwners(propagatingOwners);
            }

            var pendingPropagatingOwners = _packageOwnerRequestRepository.GetAll()
                .Where(po => po.PackageRegistrationKey == package.Key && RequireSecurePushForCoOwnersPolicy.IsSubscribed(po.NewOwner))
                .Select(po => po.NewOwner.Username);
            if (pendingPropagatingOwners.Any())
            {
                return SecurePushMessages.NoticeOfPoliciesRequiredByPendingCoOwners(pendingPropagatingOwners);
            }

            return string.Empty;
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
                return Task.FromResult(Json(new { success = false, message = "Package not found." }));
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return Task.FromResult(Json(new { success = false, message = "You are not the package owner." }));
            }
            var user = _userService.FindByUsername(username);
            if (user == null)
            {
                return Task.FromResult(Json(new { success = false, message = "Owner not found." }));
            }
            if (!user.Confirmed)
            {
                return Task.FromResult(Json(new { success = false, message = string.Format(CultureInfo.InvariantCulture, "Sorry, {0} hasn't verified their email account yet and we cannot proceed with the request.", username) }));
            }
            var currentUser = _userService.FindByUsername(HttpContext.User.Identity.Name);
            if (currentUser == null)
            {
                return Task.FromResult(Json(new { success = false, message = "Current user not found." }));
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