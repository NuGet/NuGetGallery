// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class SiteAdminsController : AdminControllerBase
    {
        private readonly IUserService _userService;

        public SiteAdminsController(IUserService userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        [HttpGet]
        public ActionResult Index()
        {
            return View(
                new SiteAdminsViewModel
                {
                    AdminUsernames = _userService.GetSiteAdmins().Select(u => u.Username)
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> AddAdmin(string username)
        {
            return SetIsAdministrator(username, true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> RemoveAdmin(string username)
        {
            return SetIsAdministrator(username, false);
        }

        private async Task<ActionResult> SetIsAdministrator(string username, bool isAdmin)
        {
            var user = _userService.FindByUsername(username);
            if (user == null)
            {
                TempData["ErrorMessage"] = $"User '{username}' does not exist!";
            }
            else
            {
                await _userService.SetIsAdministrator(user, isAdmin);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}