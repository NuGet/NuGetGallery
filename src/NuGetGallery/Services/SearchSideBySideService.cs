// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.OData;

namespace NuGetGallery
{
    public class SearchSideBySideService : ISearchSideBySideService
    {
        private readonly ISearchService _oldSearchService;
        private readonly ISearchService _newSearchService;
        private readonly ITelemetryService _telemetryService;
        private readonly IMessageService _messageService;
        private readonly IMessageServiceConfiguration _messageServiceConfiguration;
        private readonly IIconUrlProvider _iconUrlProvider;
        private readonly ListPackageItemViewModelFactory _listPackageItemViewModelFactory;

        public SearchSideBySideService(
            ISearchService oldSearchService,
            ISearchService newSearchService,
            ITelemetryService telemetryService,
            IMessageService messageService,
            IMessageServiceConfiguration messageServiceConfiguration,
            IIconUrlProvider iconUrlProvider)
        {
            _oldSearchService = oldSearchService ?? throw new ArgumentNullException(nameof(oldSearchService));
            _newSearchService = newSearchService ?? throw new ArgumentNullException(nameof(newSearchService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _messageServiceConfiguration = messageServiceConfiguration ?? throw new ArgumentNullException(nameof(messageServiceConfiguration));
            _iconUrlProvider = iconUrlProvider ?? throw new ArgumentNullException(nameof(iconUrlProvider));

            _listPackageItemViewModelFactory = new ListPackageItemViewModelFactory(_iconUrlProvider);
        }

        public async Task<SearchSideBySideViewModel> SearchAsync(string searchTerm, User currentUser)
        {
            SearchSideBySideViewModel viewModel;
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();

                var oldTask = SearchAsync(_oldSearchService, searchTerm);
                var newTask = SearchAsync(_newSearchService, searchTerm);

                await Task.WhenAll(oldTask, newTask);

                var oldResults = await oldTask;
                var newResults = await newTask;

                viewModel = new SearchSideBySideViewModel
                {
                    SearchTerm = searchTerm,
                    OldSuccess = SearchResults.IsSuccessful(oldResults),
                    OldHits = oldResults.Hits,
                    OldItems = oldResults.Data.Select(x => _listPackageItemViewModelFactory.Create(x, currentUser)).ToList(),
                    NewSuccess = SearchResults.IsSuccessful(newResults),
                    NewHits = newResults.Hits,
                    NewItems = newResults.Data.Select(x => _listPackageItemViewModelFactory.Create(x, currentUser)).ToList(),
                };

                _telemetryService.TrackSearchSideBySide(
                    viewModel.SearchTerm,
                    viewModel.OldSuccess,
                    viewModel.OldHits,
                    viewModel.NewSuccess,
                    viewModel.NewHits);
            }
            else
            {
                viewModel = new SearchSideBySideViewModel();
            }

            viewModel.EmailAddress = currentUser?.EmailAddress;

            return viewModel;
        }

        public async Task RecordFeedbackAsync(SearchSideBySideViewModel viewModel, string searchUrl)
        {
            _telemetryService.TrackSearchSideBySideFeedback(
                Trim(viewModel.SearchTerm),
                viewModel.OldHits,
                viewModel.NewHits,
                Trim(viewModel.BetterSide),
                Trim(viewModel.MostRelevantPackage),
                Trim(viewModel.ExpectedPackages),
                !string.IsNullOrWhiteSpace(viewModel.Comments),
                !string.IsNullOrWhiteSpace(viewModel.EmailAddress));

            await _messageService.SendMessageAsync(
                new SearchSideBySideMessage(_messageServiceConfiguration, viewModel, searchUrl));
        }

        private static string Trim(string input)
        {
            return input?.Trim() ?? string.Empty;
        }

        private async Task<SearchResults> SearchAsync(ISearchService searchService, string searchTerm)
        {
            await Task.Yield();

            var searchFilter = SearchAdaptor.GetSearchFilter(
                searchTerm,
                page: 1,
                includePrerelease: true,
                packageType: null,
                sortOrder: null,
                context: SearchFilter.UISearchContext,
                semVerLevel: SemVerLevelKey.SemVerLevel2);

            searchFilter.Take = 10;

            return await searchService.Search(searchFilter);
        }
    }
}