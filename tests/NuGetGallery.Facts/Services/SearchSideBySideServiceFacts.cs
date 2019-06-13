// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Infrastructure.Search.Models;
using Xunit;

namespace NuGetGallery
{
    public class SearchSideBySideServiceFacts
    {
        public class SearchAsync : Facts
        {
            [Fact]
            public async Task ReturnsNullEmailAddressWithNoCurrentUser()
            {
                var result = await Target.SearchAsync("json", currentUser: null);

                Assert.Null(result.EmailAddress);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("  ")]
            public async Task ReturnsEmptyModelWithNoSearchTerm(string searchTerm)
            {
                var result = await Target.SearchAsync(searchTerm, CurrentUser);

                Assert.Equal(string.Empty, result.SearchTerm);
                Assert.Equal("me@example.com", result.EmailAddress);

                OldSearchService.Verify(x => x.Search(It.IsAny<SearchFilter>()), Times.Never);
                NewSearchService.Verify(x => x.Search(It.IsAny<SearchFilter>()), Times.Never);
                TelemetryService.Verify(
                    x => x.TrackSearchSideBySide(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int>()),
                    Times.Never);
            }

            [Fact]
            public async Task ReturnsSearchResults()
            {
                var searchTerm = " json  ";

                var result = await Target.SearchAsync(searchTerm, CurrentUser);

                Assert.Equal("json", result.SearchTerm);
                Assert.True(result.OldSuccess, "The old search should have succeeded.");
                Assert.Equal(OldSearchResults.Hits, result.OldHits);
                var oldA = Assert.Single(result.OldItems);
                Assert.Equal("1.0.0", oldA.Version);
                Assert.True(result.NewSuccess, "The new search should have succeeded.");
                Assert.Equal(NewSearchResults.Hits, result.NewHits);
                Assert.Equal(2, result.NewItems.Count);
                Assert.Equal("2.0.0", result.NewItems[0].Version);
                Assert.Equal("3.0.0", result.NewItems[1].Version);
                Assert.Equal("me@example.com", result.EmailAddress);

                OldSearchService.Verify(x => x.Search(It.IsAny<SearchFilter>()), Times.Once);
                NewSearchService.Verify(x => x.Search(It.IsAny<SearchFilter>()), Times.Once);
                TelemetryService.Verify(x => x.TrackSearchSideBySide("json", true, 1, true, 2), Times.Once);

                AssertSearchFilters(result.SearchTerm);
            }

            private void AssertSearchFilters(string searchTerm)
            {
                AssertSearchFilter(OldSearchFilters, searchTerm);
                AssertSearchFilter(NewSearchFilters, searchTerm);
            }

            private void AssertSearchFilter(List<SearchFilter> searchFilters, string searchTerm)
            {
                var single = Assert.Single(searchFilters);
                Assert.Equal(searchTerm, single.SearchTerm);
                Assert.True(single.IncludePrerelease);
                Assert.Equal(0, single.Skip);
                Assert.Equal(10, single.Take);
                Assert.Equal(SortOrder.Relevance, single.SortOrder);
                Assert.Equal(SemVerLevelKey.SemVerLevel2, single.SemVerLevel);
            }
        }

        public class RecordFeedbackAsync : Facts
        {
            [Fact]
            public async Task RecordsFeedback()
            {
                var model = new SearchSideBySideViewModel
                {
                    SearchTerm = " json ",
                    OldHits = 23,
                    NewHits = 42,
                    BetterSide = " new ",
                    MostRelevantPackage = " NuGet.Core ",
                    ExpectedPackages = " NuGet.Packaging,  NuGet.Versioning ",
                    Comments = " comments ",
                    EmailAddress = " me@example.com ",
                };
                var searchUrl = "https://localhost/experiments/search-sxs?q=json";

                await Target.RecordFeedbackAsync(model, searchUrl);

                TelemetryService.Verify(
                    x => x.TrackSearchSideBySideFeedback(
                        "json",
                        23,
                        42,
                        "new",
                        "NuGet.Core",
                        "NuGet.Packaging,  NuGet.Versioning",
                        true,
                        true),
                    Times.Once);
                MessageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<SearchSideBySideMessage>(), false, false),
                    Times.Once);
            }
        }

        public abstract class Facts
        {
            public Facts()
            {
                OldSearchService = new Mock<ISearchService>();
                NewSearchService = new Mock<ISearchService>();
                TelemetryService = new Mock<ITelemetryService>();
                MessageService = new Mock<IMessageService>();
                MessageServiceConfiguration = new Mock<IMessageServiceConfiguration>();

                CurrentUser = new User
                {
                    EmailAddress = "me@example.com",
                };
                OldSearchFilters = new List<SearchFilter>();
                OldSearchResults = new SearchResults(
                    hits: 1,
                    indexTimestampUtc: null,
                    data: new[]
                    {
                        new Package
                        {
                            Version = "1.0.0",
                            PackageRegistration = new PackageRegistration(),
                        },
                    }.AsQueryable());
                NewSearchFilters = new List<SearchFilter>();
                NewSearchResults = new SearchResults(
                    hits: 2,
                    indexTimestampUtc: null,
                    data: new[]
                    {
                        new Package
                        {
                            Version = "2.0.0",
                            PackageRegistration = new PackageRegistration(),
                        },
                        new Package
                        {
                            Version = "3.0.0",
                            PackageRegistration = new PackageRegistration(),
                        },
                    }.AsQueryable());

                OldSearchService
                    .Setup(x => x.Search(It.IsAny<SearchFilter>()))
                    .ReturnsAsync(() => OldSearchResults)
                    .Callback<SearchFilter>(x => OldSearchFilters.Add(x));
                NewSearchService
                    .Setup(x => x.Search(It.IsAny<SearchFilter>()))
                    .ReturnsAsync(() => NewSearchResults)
                    .Callback<SearchFilter>(x => NewSearchFilters.Add(x));

                Target = new SearchSideBySideService(
                    OldSearchService.Object,
                    NewSearchService.Object,
                    TelemetryService.Object,
                    MessageService.Object,
                    MessageServiceConfiguration.Object);
            }

            public Mock<ISearchService> OldSearchService { get; }
            public Mock<ISearchService> NewSearchService { get; }
            public Mock<ITelemetryService> TelemetryService { get; }
            public Mock<IMessageService> MessageService { get; }
            public Mock<IMessageServiceConfiguration> MessageServiceConfiguration { get; }
            public User CurrentUser { get; }
            public List<SearchFilter> OldSearchFilters { get; }
            public SearchResults OldSearchResults { get; }
            public List<SearchFilter> NewSearchFilters { get; }
            public SearchResults NewSearchResults { get; }
            public SearchSideBySideService Target { get; }
        }
    }
}
