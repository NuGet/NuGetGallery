// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Auditing;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ChangeUsernameController : AdminControllerBase
    {
        private readonly IUserService _userService;
        private readonly IEntityRepository<User> _userRepository;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IAuditingService _auditingService;
        private readonly IPackageService _packageService;

        private readonly Regex UsernameValidationRegex = new Regex(GalleryConstants.UsernameValidationRegex);

        public ChangeUsernameController(
            IUserService userService,
            IEntityRepository<User> userRepository,
            IEntitiesContext entitiesContext,
            IDateTimeProvider dateTimeProvider,
            IAuditingService auditingService,
            IPackageService packageService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        }

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult VerifyAccount(string accountEmailOrUsername)
        {
            if (string.IsNullOrEmpty(accountEmailOrUsername))
            {
                return Json(HttpStatusCode.BadRequest, "Email or username cannot be null or empty.", JsonRequestBehavior.AllowGet);
            }

            var account = _userService.FindByUsername(accountEmailOrUsername) ?? _userService.FindByEmailAddress(accountEmailOrUsername);

            if (account == null)
            {
                return Json(HttpStatusCode.NotFound, "Account was not found.", JsonRequestBehavior.AllowGet);
            }

            var result = new ValidateAccountResult();
            result.Administrators = new List<ValidateAccount>();
            result.Account = new ValidateAccount()
            {
                Username = account.Username,
                EmailAddress = account.EmailAddress
            };

            if (account is Organization)
            {
                foreach (var admin in (account as Organization).Administrators)
                {
                    var owner = new ValidateAccount()
                    {
                        Username = admin.Username,
                        EmailAddress = admin.EmailAddress
                    };
                    result.Administrators.Add(owner);
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult ValidateNewUsername(string newUsername, bool checkOwnedPackages, string oldUsername)
        {
            if (string.IsNullOrEmpty(newUsername))
            {
                return Json(HttpStatusCode.BadRequest, "Username cannot be null or empty.", JsonRequestBehavior.AllowGet);
            }

            if (string.IsNullOrEmpty(oldUsername))
            {
                return Json(HttpStatusCode.BadRequest, "Old username cannot be null or empty.", JsonRequestBehavior.AllowGet);
            }

            var oldAccount = _userService.FindByUsername(oldUsername);
            if (oldAccount == null)
            {
                return Json(HttpStatusCode.NotFound, "Old username account was not found.", JsonRequestBehavior.AllowGet);
            }

            var result = ValidateUsernameChange(oldAccount, newUsername);

            if (checkOwnedPackages)
            {
                var ownedPackages = _packageService.FindPackagesByOwner(oldAccount, includeUnlisted: true)
                    .Where(p => p.PackageStatusKey != PackageStatus.Deleted)
                    .Select(p => p.PackageRegistration.Id);
                result.OwnedPackageIds = ownedPackages;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeUsername(string oldUsername, string newUsername)
        {
            if (string.IsNullOrEmpty(oldUsername))
            {
                return Json(HttpStatusCode.BadRequest, "Old username cannot be null or empty.", JsonRequestBehavior.AllowGet);
            }

            if (string.IsNullOrEmpty(newUsername))
            {
                return Json(HttpStatusCode.BadRequest, "New username cannot be null or empty.", JsonRequestBehavior.AllowGet);
            }

            var account = _userService.FindByUsername(oldUsername);

            if (account == null)
            {
                return Json(HttpStatusCode.NotFound, "Old username account was not found.", JsonRequestBehavior.AllowGet);
            }

            var newUsernameValidation = ValidateUsernameChange(account, newUsername);

            if (!newUsernameValidation.IsFormatValid || !newUsernameValidation.IsAvailable)
            {
                return Json(HttpStatusCode.BadRequest, "New username validation failed.", JsonRequestBehavior.AllowGet);
            }

            if (account.Username.Equals(newUsername, StringComparison.OrdinalIgnoreCase) == false)
            {
                // We're doing a full username change and not just a casing change so we need to lock the old username
                var newAccountForOldUsername = new User()
                {
                    Username = account.Username,
                    EmailAllowed = false,
                    IsDeleted = true,
                    CreatedUtc = _dateTimeProvider.UtcNow
                };

                _userRepository.InsertOnCommit(newAccountForOldUsername);
            }

            account.Username = newUsername;

            await _auditingService.SaveAuditRecordAsync(new UserAuditRecord(account, AuditedUserAction.ChangeUsername));
            await _entitiesContext.SaveChangesAsync();

            return Json(HttpStatusCode.OK, "Account renamed successfully!", JsonRequestBehavior.AllowGet);
        }

        private ValidateUsernameResult ValidateUsernameChange(User requestor, string username)
        {
            var foundUser = _userService.FindByUsername(username, includeDeleted: true);

            return new ValidateUsernameResult()
            {
                IsFormatValid = UsernameValidationRegex.IsMatch(username),
                IsAvailable = foundUser == null || (requestor.Key == foundUser.Key && foundUser.Username != username) // The username check is in the event where we found a user in the DB but we're doing a cAsIng change
            };
        }
    }
}
