// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.Services;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Services.Telemetry;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class RevalidationController : AdminControllerBase
    {
        private readonly RevalidationAdminService _revalidationAdminService;
        private readonly IRevalidationStateService _state;

        public RevalidationController(RevalidationAdminService revalidationAdminService, IRevalidationStateService state)
        {
            _revalidationAdminService = revalidationAdminService ?? throw new ArgumentNullException(nameof(revalidationAdminService));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        [HttpGet]
        public async virtual Task<ActionResult> Index()
        {
            var state = await _state.GetStateAsync();
            var statistics = _revalidationAdminService.GetStatistics();

            return View(nameof(Index), new RevalidationPageViewModel(state, statistics));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SetKillswitch(bool killswitch)
        {
            try
            {
                await _state.UpdateStateAsync(state => state.IsKillswitchActive = killswitch);
            }
            catch (Exception e)
            {
                TempData["ErrorMessage"] = $"Failed to update revalidation state due to exception: {e.Message}";
                QuietLog.LogHandledException(e);
            }

            return Redirect(Url.Action(actionName: "Index", controllerName: "Revalidation"));
        }
    }
}