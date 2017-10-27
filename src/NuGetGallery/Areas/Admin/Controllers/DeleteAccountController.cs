// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            _userService = userService;
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            var model = new DeleteAccountRequest
            {
            };
            return View(model);
        }

        [HttpGet]
        public virtual ActionResult Search(string query)
        {
            var results = new List<DeleteAccountSearchResult>();
            var result = new DeleteAccountSearchResult();
            if (query != null)
            {
                var user = _userService.FindByUsername(query);
                if (user != null)
                {
                    result.AccountName = user.Username;
                }
            }
            if(result.AccountName!=null)
            {
                results.Add(result);
            }
            return Json( results, JsonRequestBehavior.AllowGet);
        }
    }
}