// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ChangeUsernameController : AdminControllerBase
    {
        private readonly IUserService _userService;

        public ChangeUsernameController(IUserService userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public virtual ActionResult VerifyAccount(string accountEmailOrUsername)
        {
            if (string.IsNullOrEmpty(accountEmailOrUsername))
            {
                return HttpNotFound();
            }

            var result = new ValidateAccountResult();
            result.Administrators = new List<ValidateAccount>();

            var account = _userService.FindByUsername(accountEmailOrUsername);

            if (account is null)
            {
                account = _userService.FindByEmailAddress(accountEmailOrUsername);

                if(account is null)
                {
                    return HttpNotFound();
                }
            }

            result.Account = new ValidateAccount() { Username = account.Username, EmailAddress = account.EmailAddress };

            if (account is Organization)
            {
                foreach (var admin in (account as Organization).Administrators)
                {
                    var owner = new ValidateAccount() { Username = admin.Username, EmailAddress = admin.EmailAddress };
                    result.Administrators.Add(owner);
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public virtual ActionResult ValidateNewUsername(string newUsername)
        {
            var validationResult = new ValidateAccountResult();

            var newUser = _userService.FindByUsername(newUsername);


            return Json(validationResult, JsonRequestBehavior.AllowGet);
        }
    }
}
