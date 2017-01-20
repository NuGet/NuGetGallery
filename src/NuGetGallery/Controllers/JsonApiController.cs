﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

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

        [HttpPost]
        public async Task<JsonResult> AddPackageOwner(string id, string username, string message)
        {
            message = HttpUtility.HtmlEncode(message);

            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                return Json(new { success = false, message = "Package not found." });
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return Json(new { success = false, message = "You are not the package owner." });
            }
            var user = _userService.FindByUsername(username);
            if (user == null)
            {
                return Json(new { success = false, message = "Owner not found." });
            }
            if (!user.Confirmed)
            {
                return Json(new { success = false, message = string.Format(CultureInfo.InvariantCulture, "Sorry, {0} hasn't verified their email account yet and we cannot proceed with the request.", username) });
            }

            var currentUser = _userService.FindByUsername(HttpContext.User.Identity.Name);
            var ownerRequest = await _packageService.CreatePackageOwnerRequestAsync(package, currentUser, user);

            var confirmationUrl = Url.ConfirmationUrl(
                "ConfirmOwner",
                "Packages",
                user.Username,
                ownerRequest.ConfirmationCode,
                new { id = package.Id });
            _messageService.SendPackageOwnerRequest(currentUser, user, package, confirmationUrl, message);

            return Json(new { success = true, name = user.Username, pending = true });
        }

        [HttpPost]
        public async Task<JsonResult> RemovePackageOwner(string id, string username)
        {
            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                return Json(new { success = false, message = "Package not found" });
            }
            if (!package.IsOwner(HttpContext.User))
            {
                return Json(new { success = false, message = "You are not the package owner." });
            }
            var user = _userService.FindByUsername(username);
            if (user == null)
            {
                return Json(new { success = false, message = "Owner not found" });
            }
            var currentUser = _userService.FindByUsername(HttpContext.User.Identity.Name);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "Current user not found" });
            }

            await _packageService.RemovePackageOwnerAsync(package, user);
            _messageService.SendPackageOwnerRemovedNotice(currentUser, user, package);

            return Json(new { success = true });
        }

        public class OwnerModel
        {
            public string name { get; set; }
            public bool current { get; set; }
            public bool pending { get; set; }
        }
    }
}