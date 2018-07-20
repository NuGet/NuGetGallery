// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web.Mvc;
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
                var user = _userService.FindByUsername(query);
                if (user !=  null && user.Username != null && !user.IsDeleted)
                {
                    var result = new DeleteAccountSearchResult(user.Username);
                    results.Add(result);
                }
            }
           
            return Json(results, JsonRequestBehavior.AllowGet);
        }
    }
}