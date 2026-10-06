// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Filters;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class NamespaceReservationRequestsControllerFacts
    {
        [Fact]
        public void RequiresRepository()
        {
            Assert.Throws<ArgumentNullException>(() => new NamespaceReservationRequestsController(null));
        }

        [Fact]
        public void InheritsAdminAuthorizationAndExposesOnlyGetListing()
        {
            var authorization = Assert.Single(typeof(NamespaceReservationRequestsController)
                .GetCustomAttributes(typeof(UIAuthorizeAttribute), true).Cast<UIAuthorizeAttribute>());
            Assert.Equal("Admins", authorization.Roles);
            var action = typeof(NamespaceReservationRequestsController).GetMethod(nameof(NamespaceReservationRequestsController.Index));
            Assert.True(Attribute.IsDefined(action, typeof(HttpGetAttribute)));
        }

        public class TheIndexAction
        {
            [Theory]
            [InlineData("", "2026-09-16", 1, "fromDate")]
            [InlineData("invalid", "2026-09-16", 1, "fromDate")]
            [InlineData("09/16/2026", "2026-09-16", 1, "fromDate")]
            [InlineData("2026-02-29", "2026-09-16", 1, "fromDate")]
            [InlineData("2026-09-16T12:00:00Z", "2026-09-16", 1, "fromDate")]
            [InlineData("2026-09-16", "", 1, "toDate")]
            [InlineData("2026-09-16", "invalid", 1, "toDate")]
            [InlineData("2026-09-17", "2026-09-16", 1, "toDate")]
            [InlineData("2026-09-16", "2026-09-16", 0, "page")]
            [InlineData("2026-09-16", "2026-09-16", -1, "page")]
            [InlineData("2026-09-16", "2026-09-16", int.MaxValue, "page")]
            public void RejectsInvalidFiltersWithoutQuerying(string from, string to, int page, string errorField)
            {
                var repository = new Mock<IEntityRepository<NamespaceReservationRequest>>(MockBehavior.Strict);
                var controller = new NamespaceReservationRequestsController(repository.Object);

                var model = GetModel(controller.Index(from, to, page));

                Assert.False(controller.ModelState.IsValid);
                Assert.NotEmpty(controller.ModelState[errorField].Errors);
                Assert.Equal(from, model.FromDate);
                Assert.Equal(to, model.ToDate);
                Assert.False(model.HasResults);
                Assert.Empty(model.Requests);
            }

            [Fact]
            public void RejectsModelBindingErrorsWithoutQuerying()
            {
                var repository = new Mock<IEntityRepository<NamespaceReservationRequest>>(MockBehavior.Strict);
                var controller = new NamespaceReservationRequestsController(repository.Object);
                controller.ModelState.AddModelError("page", "Invalid page.");

                Assert.False(GetModel(controller.Index("2026-09-16", "2026-09-16")).HasResults);
            }

            [Fact]
            public void DefaultsToLastSevenUtcDates()
            {
                var before = DateTime.UtcNow.Date;
                var controller = CreateController(Array.Empty<NamespaceReservationRequest>());

                var model = GetModel(controller.Index());

                var to = DateTime.ParseExact(model.ToDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                Assert.InRange(to, before, DateTime.UtcNow.Date);
                Assert.Equal(to.AddDays(-6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), model.FromDate);
                Assert.True(model.HasResults);
                Assert.Empty(model.Requests);
                Assert.False(model.HasNextPage);
                Assert.Equal(1, model.Page);
            }

            [Fact]
            public void IncludesEntireEndDateAndOrdersOldestFirstThenAscendingKey()
            {
                var start = new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc);
                var end = start.AddDays(1);
                var controller = CreateController(new[]
                {
                    Request(1, start.AddTicks(-1)),
                    Request(2, start),
                    Request(3, end.AddTicks(-1)),
                    Request(4, end),
                    Request(5, start)
                });

                var model = GetModel(controller.Index("2026-09-16", "2026-09-16"));

                Assert.Equal(new[] { 2, 5, 3 }, model.Requests.Select(r => r.Key));
                Assert.True(controller.ModelState.IsValid);
            }

            [Fact]
            public void MaximumEndDateDoesNotOverflow()
            {
                var controller = CreateController(new[] { Request(1, DateTime.MaxValue) });

                Assert.Single(GetModel(controller.Index("9999-12-31", "9999-12-31")).Requests);
                Assert.True(controller.ModelState.IsValid);
            }

            [Fact]
            public void ReturnsBoundedPagesWithoutDroppingEqualTimestampRows()
            {
                var date = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
                var controller = CreateController(Enumerable.Range(1, 51).Select(i => Request(i, date)));

                var first = GetModel(controller.Index("2026-09-16", "2026-09-16"));
                var second = GetModel(controller.Index("2026-09-16", "2026-09-16", 2));
                var empty = GetModel(controller.Index("2026-09-16", "2026-09-16", 3));

                Assert.Equal(50, first.Requests.Count);
                Assert.True(first.HasNextPage);
                Assert.Equal(1, first.Requests.First().Key);
                Assert.Equal(50, first.Requests.Last().Key);
                Assert.Equal(51, Assert.Single(second.Requests).Key);
                Assert.False(second.HasNextPage);
                Assert.Equal(2, second.Page);
                Assert.Empty(empty.Requests);
                Assert.False(empty.HasNextPage);
            }

            [Fact]
            public void FullLastPageDoesNotClaimAnotherPageExists()
            {
                var date = new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc);
                var controller = CreateController(Enumerable.Range(1, 50).Select(i => Request(i, date)));

                var model = GetModel(controller.Index("2026-09-16", "2026-09-16"));

                Assert.Equal(50, model.Requests.Count);
                Assert.False(model.HasNextPage);
            }

            [Theory]
            [InlineData("Pending", false)]
            [InlineData("Approved", true)]
            [InlineData("Rejected", true)]
            public void DisplaysPersistedOutcomesWithoutChangingRequests(string status, bool completed)
            {
                var date = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
                var request = Request(1, date);
                request.Status = status;
                request.CompletedTimestamp = completed ? date.AddMinutes(1) : (DateTime?)null;
                var repository = new Mock<IEntityRepository<NamespaceReservationRequest>>(MockBehavior.Strict);
                repository.Setup(r => r.GetAll()).Returns(new[] { request }.AsQueryable());
                var controller = new NamespaceReservationRequestsController(repository.Object);

                var row = Assert.Single(GetModel(controller.Index("2026-09-16", "2026-09-16")).Requests);

                Assert.Equal(request.Namespace, row.Namespace);
                Assert.Equal("Alice", row.SubmittedBy);
                Assert.Equal(request.RequestedOwnersJson, row.RequestedOwnersJson);
                Assert.Equal(new[] { "ExampleOrg" }, row.RequestedOwners);
                Assert.Equal(request.Justification, row.Justification);
                Assert.Equal(status, row.Status);
                Assert.Equal(request.Reason, row.Reason);
                Assert.Equal(request.CompletedTimestamp, row.CompletedTimestamp);
                Assert.Equal(status, request.Status);
                repository.Verify(r => r.GetAll(), Times.Once);
                repository.VerifyNoOtherCalls();
            }
        }

        public class TheStoredDataProjection
        {
            [Fact]
            public void FormatsOwnersAsReadableNamesWithoutJsonOrKeys()
            {
                var row = new NamespaceReservationRequestRowViewModel
                {
                    RequestedOwnersJson = "[{\"Key\":123,\"Username\":\"Alice\"},{\"Key\":456,\"Username\":\"ExampleOrg\"}]"
                };

                Assert.Equal(new[] { "Alice", "ExampleOrg" }, row.RequestedOwners);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("[]")]
            public void EmptyOwnerSnapshotsHaveNoNames(string json)
            {
                Assert.Empty(new NamespaceReservationRequestRowViewModel { RequestedOwnersJson = json }.RequestedOwners);
            }

            [Theory]
            [InlineData("not json", "Owner information unavailable")]
            [InlineData("{}", "Owner information unavailable")]
            [InlineData("[null]", "Unknown owner")]
            [InlineData("[{\"Key\":123}]", "Unknown owner")]
            [InlineData("[{\"Username\":456}]", "Unknown owner")]
            public void InvalidOwnerSnapshotsHaveReadableFallbacks(string json, string expected)
            {
                Assert.Equal(expected, Assert.Single(new NamespaceReservationRequestRowViewModel { RequestedOwnersJson = json }.RequestedOwners));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("not json")]
            [InlineData("{}")]
            [InlineData("[]")]
            [InlineData("[null]")]
            [InlineData("[{\"Key\":123}]")]
            [InlineData("[{\"Username\":456}]")]
            [InlineData("[{\"Key\":123,\"Username\":\"Alice\",\"EmailAddress\":\"private@example.test\"},{\"Key\":456,\"Username\":\"ExampleOrg\"}]")]
            public void PreservesFullOwnerSnapshotWithoutParsingOrRedacting(string json)
            {
                var request = Request(1, new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc));
                request.RequestedOwnersJson = json;
                var controller = CreateController(new[] { request });

                var row = Assert.Single(GetModel(controller.Index("2026-09-16", "2026-09-16")).Requests);

                Assert.Equal(json, row.RequestedOwnersJson);
            }

            [Fact]
            public void PreservesCompleteTextAndTimestampsForHtmlEncodedRendering()
            {
                var request = Request(1, new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc).AddTicks(1234567));
                request.Justification = "<script>alert('untrusted')</script>\n" + new string('x', 4000);
                request.Reason = "Contact alice@example.test\nFull saved reason.";
                request.CompletedTimestamp = request.CreatedTimestamp.AddMinutes(1);
                var controller = CreateController(new[] { request });

                var row = Assert.Single(GetModel(controller.Index("2026-09-16", "2026-09-16")).Requests);

                Assert.Equal(request.Justification, row.Justification);
                Assert.Equal(request.Reason, row.Reason);
                Assert.Equal(request.CreatedTimestamp, row.CreatedTimestamp);
                Assert.Equal(request.CompletedTimestamp, row.CompletedTimestamp);
            }
        }

        private static NamespaceReservationRequestsController CreateController(IEnumerable<NamespaceReservationRequest> requests)
        {
            var repository = new Mock<IEntityRepository<NamespaceReservationRequest>>(MockBehavior.Strict);
            repository.Setup(r => r.GetAll()).Returns(requests.AsQueryable());
            return new NamespaceReservationRequestsController(repository.Object);
        }

        private static NamespaceReservationRequestsViewModel GetModel(ActionResult result)
        {
            return Assert.IsType<NamespaceReservationRequestsViewModel>(Assert.IsType<ViewResult>(result).Model);
        }

        private static NamespaceReservationRequest Request(int key, DateTime created)
        {
            return new NamespaceReservationRequest
            {
                Key = key,
                Namespace = "Example.Product",
                SubmittedByUserKey = 42,
                SubmittedByUser = new User("Alice") { Key = 42 },
                RequestedOwnersJson = "[{\"Key\":2,\"Username\":\"ExampleOrg\"}]",
                Justification = "Our product.",
                CreatedTimestamp = created,
                Status = "Pending",
                Reason = "Awaiting assessment."
            };
        }
    }
}