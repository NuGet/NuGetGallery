// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.Build.Utilities;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public partial class DeleteAccountController : AdminControllerBase
    {
        private readonly IUserService _userService;

        public DeleteAccountController(IUserService userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public virtual ActionResult Search(string query)
        {
            var results = new List<DeleteAccountSearchResult>();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var user = _userService.FindByUsername(query, includeDeleted: true);
                if (user !=  null && user.Username != null)
                {
                    var result = new DeleteAccountSearchResult(
                        user.Username, 
                        user.IsDeleted,
                        Url.User(user),
                        user.IsDeleted 
                            ? null 
                            : (user is Organization ? Url.AdminDeleteOrganization(user.Username) : Url.AdminDeleteAccount(user.Username)),
                        user.IsDeleted 
                            ? Url.AdminRenameDeletedAccount(user.Username) 
                            : null);
                    results.Add(result);
                }
            }
           
            return Json(results, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public virtual async Task<ActionResult> Rename(string accountName)
        {
            try
            {
                var user = _userService.FindByUsername(accountName, includeDeleted: true);
                await _userService.RenameDeletedAccount(user);
                TempData["Message"] = $"The account named {accountName} has been successfully renamed.";
            }
            catch (UserSafeException e)
            {
                TempData["ErrorMessage"] = e.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}