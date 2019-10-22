// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Controllers
{
    public class ExperimentsController : AppController
    {
        private readonly ISearchSideBySideService _searchSideBySideService;
        private readonly IFeatureFlagService _featureFlagService;

        public ExperimentsController(
            ISearchSideBySideService searchSideBySideService,
            IFeatureFlagService featureFlagService)
        {
            _searchSideBySideService = searchSideBySideService ?? throw new ArgumentNullException(nameof(searchSideBySideService));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
        }

        [HttpGet]
        public async Task<ActionResult> SearchSideBySide(string q = null)
        {
            var currentUser = GetCurrentUser();
            if (!_featureFlagService.IsSearchSideBySideEnabled(currentUser))
            {
                return View(new SearchSideBySideViewModel { IsDisabled = true });
            }

            var viewModel = await _searchSideBySideService.SearchAsync(q, currentUser);

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SearchSideBySide(SearchSideBySideViewModel input)
        {
            var currentUser = GetCurrentUser();
            if (!_featureFlagService.IsSearchSideBySideEnabled(currentUser))
            {
                return new HttpNotFoundResult();
            }

            var searchUrl = Url.SearchSideBySide(searchTerm: input.SearchTerm?.Trim(), relativeUrl: false);
            await _searchSideBySideService.RecordFeedbackAsync(input, searchUrl);

            TempData["Message"] = "Thank you for providing feedback! Feel free to try some other queries.";

            return RedirectToAction(nameof(SearchSideBySide));
        }
    }
}