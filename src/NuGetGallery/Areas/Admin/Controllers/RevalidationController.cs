// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.Services;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class RevalidationController : AdminControllerBase
    {
        private readonly RevalidationAdminService _revalidationAdminService;
        private readonly IRevalidationSettingsService _settings;

        public RevalidationController(RevalidationAdminService revalidationAdminService, IRevalidationSettingsService settings)
        {
            _revalidationAdminService = revalidationAdminService ?? throw new ArgumentNullException(nameof(revalidationAdminService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        [HttpGet]
        public async virtual Task<ActionResult> Index()
        {
            var settings = await _settings.GetSettingsAsync();
            var statistics = await _revalidationAdminService.GetStatisticsAsync();

            return View(nameof(Index), new RevalidationPageViewModel(settings, statistics));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ActivateKillswitch(bool killswitch)
        {
            await _settings.UpdateSettingsAsync(settings => settings.Killswitch = killswitch);

            return Redirect(Url.Action(actionName: "Index", controllerName: "Revalidation"));
        }
    }
}