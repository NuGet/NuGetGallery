// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace NuGetGallery.FunctionalTests.Playwright.AccountManagement
{
    [Collection(GalleryTestCollection.Definition)]
    public class NamespaceRequestUITest : NuGetPageTest
    {
        private const string SubmittedMessage = "Your request has been submitted successfully. It is being processed. Please check back later.";

        [Fact]
        [Priority(1)]
        [Category("P1Tests")]
        [Category("PlaywrightTests")]
        public async Task ManagePackages_ShowsCombinedNamespaceReservationSectionAfterUnlistedPackages()
        {
            // Arrange
            await SignInAsync();

            // Act
            await Page.GotoAsync(UrlHelper.ManageMyPackagesUrl);

            // Assert
            await Expect(Page.Locator("script[src*='page-manage-packages']")).ToHaveAttributeAsync("src", new Regex(@"[?&]v="));
            var section = Page.Locator("#namespaces-container");
            var heading = Page.Locator("#unlisted-container + .clearfix #show-namespaces-container");
            await Expect(section).ToHaveCountAsync(1);
            await Expect(Page.Locator("#namespace-request-container")).ToHaveCountAsync(0);
            await Expect(heading).ToHaveTextAsync("Namespace Reservation");
            await Expect(heading).ToHaveAttributeAsync("aria-expanded", "false");
            await Expect(section).ToBeHiddenAsync();

            await heading.ClickAsync();
            await Expect(heading).ToHaveAttributeAsync("aria-expanded", "true");
            await Expect(section).ToBeVisibleAsync();
            await Expect(section.GetByRole(AriaRole.Heading, new() { Name = "Namespaces and requests", Exact = true })).ToHaveCountAsync(0);
            await Expect(section.Locator("hr")).ToHaveCountAsync(0);
            await Expect(section.Locator(".user-package-list + .row").GetByRole(AriaRole.Heading, new() { Name = "Request a namespace reservation", Exact = true })).ToBeVisibleAsync();
            await Expect(section.Locator("input[type=text], textarea")).ToHaveCountAsync(3);
            var guidelines = section.Locator("#namespace-request-guidelines");
            await Expect(guidelines.Locator(".alert-brand-info")).ToBeVisibleAsync();
            await Expect(guidelines.Locator(".alert-brand-info")).ToHaveCSSAsync("display", "flex");
            await Expect(section.Locator("#namespace-request-form > #namespace-request-guidelines")).ToHaveCountAsync(1);
            await Expect(guidelines.Locator("li")).ToHaveTextAsync(new[]
            {
                "You cannot submit a new request while another request is Pending.",
                "If your request is rejected, you can try again with more information or contact us.",
                "You can submit up to three reservation requests for the same namespace."
            });
            var contactUrl = await Page.Locator(".footer-heading")
                .GetByRole(AriaRole.Link, new() { Name = "Contact", Exact = true }).GetAttributeAsync("href");
            await Expect(guidelines.GetByRole(AriaRole.Link, new() { Name = "contact us", Exact = true }))
                .ToHaveAttributeAsync("href", contactUrl);

            var namespaceInput = section.GetByLabel("Namespace", new() { Exact = true });
            var ownerInput = section.GetByLabel("Owners", new() { Exact = true });
            var justificationInput = section.GetByLabel("Justification", new() { Exact = true });
            await Expect(namespaceInput).ToHaveValueAsync(string.Empty);
            await Expect(ownerInput).ToHaveValueAsync(string.Empty);
            await Expect(justificationInput).ToHaveValueAsync(string.Empty);
            await Expect(section.Locator("#namespace-request-form [placeholder]")).ToHaveCountAsync(0);

            await namespaceInput.FillAsync("Contoso");
            await ownerInput.FillAsync("alice, Contoso, MyOrganization");
            await justificationInput.FillAsync("Our packages use this name. https://example.com");

            await Expect(namespaceInput).ToHaveValueAsync("Contoso");
            await Expect(ownerInput).ToHaveValueAsync("alice, Contoso, MyOrganization");
            await Expect(justificationInput).ToHaveValueAsync("Our packages use this name. https://example.com");
            await Expect(section.Locator("#namespace-request-help")).ToHaveCountAsync(0);
            var submitButton = section.GetByRole(AriaRole.Button, new() { Name = "Submit request" });
            await Expect(submitButton).ToBeEnabledAsync();
            await Expect(submitButton).ToHaveCSSAsync("opacity", "1");
            await Expect(submitButton).ToHaveAttributeAsync("type", "submit");
            await Expect(section.Locator("form")).ToHaveCountAsync(1);
            await Expect(section.Locator("input[name=__RequestVerificationToken]")).ToHaveCountAsync(1);
            await Expect(section.Locator("#namespace-request-form > fieldset + #namespace-request-feedback")).ToBeHiddenAsync();
        }

        [Fact]
        [Priority(1)]
        [Category("P1Tests")]
        [Category("PlaywrightTests")]
        public async Task ManagePackages_InvalidNamespaceShowsErrorAndPreservesInput()
        {
            await SignInAsync();
            await Page.GotoAsync(UrlHelper.ManageMyPackagesUrl);
            await Page.Locator("#show-namespaces-container").ClickAsync();
            await Page.Locator("#namespace-request-namespace").FillAsync("Contoso.*");
            await Page.Locator("#namespace-request-owner").FillAsync(GalleryConfiguration.Instance.Account.Name);
            await Page.Locator("#namespace-request-justification").FillAsync("Our packages use this namespace.");

            await Page.GetByRole(AriaRole.Button, new() { Name = "Submit request", Exact = true }).ClickAsync();

            await Expect(Page.Locator("#namespaces-container")).ToBeVisibleAsync();
            await Expect(Page.Locator("#namespace-request-namespace-error")).ToContainTextAsync("base namespace");
            await Expect(Page.Locator("#namespace-request-namespace")).ToHaveValueAsync("Contoso.*");
            await Expect(Page.Locator("#namespace-request-justification")).ToHaveValueAsync("Our packages use this namespace.");
        }

        [Fact]
        [Priority(1)]
        [Category("P1Tests")]
        [Category("PlaywrightTests")]
        public async Task ManagePackages_DisplaysPendingThenApprovedThenReservedAfterAllocation()
        {
            await SignInAsync();
            const string namespaceValue = "Contoso.StatusTest";
            string savedStatus = null;
            var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var save = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var assessment = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var allocation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Intercept both endpoints: this test never creates a request, runs inference,
            // or allocates a namespace. The read feed represents committed server state.
            await Page.RouteAsync("**/account/NamespaceReservationStatuses*", route =>
            {
                var owners = new[] { new { Username = GalleryConfiguration.Instance.Account.Name, ProfileUrl = "/", IsOrganization = false } };
                var rows = new List<object>
                {
                    new { Pattern = "Existing.*", SearchUrl = "/", Owners = owners, IsPublic = false, Status = "Reserved" }
                };
                if (savedStatus != null)
                {
                    rows.Add(new { Pattern = namespaceValue, SearchUrl = "/", Owners = owners, IsPublic = false, Status = savedStatus });
                }
                return route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(rows) });
            });
            await Page.RouteAsync("**/account/RequestNamespaceReservation", async route =>
            {
                received.TrySetResult(true);
                await save.Task;
                savedStatus = "Pending";
                await assessment.Task;
                savedStatus = "Approved";
                await allocation.Task;
                savedStatus = "Reserved";
                await route.FulfillAsync(new()
                {
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(new { Success = true, Message = SubmittedMessage, IsWarning = false })
                });
            });

            try
            {
                await Page.GotoAsync(UrlHelper.ManageMyPackagesUrl);
                var heading = Page.Locator("#show-namespaces-container");
                await heading.ClickAsync();
                var section = Page.Locator("#namespaces-container");
                var existing = section.Locator("tbody tr").Filter(new() { HasText = "Existing.*" });
                await Expect(existing.GetByRole(AriaRole.Cell, new() { Name = "Reserved", Exact = true })).ToBeVisibleAsync();

                await section.GetByLabel("Namespace", new() { Exact = true }).FillAsync(namespaceValue);
                await section.GetByLabel("Owners", new() { Exact = true }).FillAsync(GalleryConfiguration.Instance.Account.Name);
                await section.GetByLabel("Justification", new() { Exact = true }).FillAsync("Our packages use this namespace.");
                var submit = section.GetByRole(AriaRole.Button, new() { Name = "Submit request", Exact = true });
                await submit.ClickAsync();
                await received.Task;

                var request = section.Locator("tbody tr").Filter(new() { HasText = namespaceValue });
                var feedback = section.Locator("#namespace-request-form > fieldset + #namespace-request-feedback");
                await Expect(request).ToHaveCountAsync(0);
                await Expect(feedback).ToBeHiddenAsync();
                await Expect(feedback.Locator(".alert-info, .alert-brand-info")).ToHaveCountAsync(0);

                save.SetResult(true);
                await Expect(request.GetByRole(AriaRole.Cell, new() { Name = "Pending", Exact = true })).ToBeVisibleAsync();
                await Expect(feedback).ToBeVisibleAsync();
                await Expect(feedback.Locator(".alert-brand-success")).ToBeVisibleAsync();
                await Expect(feedback.Locator(".ms-Icon--CompletedSolid")).ToBeVisibleAsync();
                await Expect(feedback).ToHaveTextAsync(SubmittedMessage);
                var infoAlert = await section.Locator("#namespace-request-guidelines .alert-brand-info").BoundingBoxAsync();
                var successAlert = await feedback.Locator(".alert-brand-success").BoundingBoxAsync();
                Assert.NotNull(infoAlert);
                Assert.NotNull(successAlert);
                Assert.InRange(Math.Abs(infoAlert.X - successAlert.X), 0, 1);
                Assert.InRange(Math.Abs(infoAlert.Width - successAlert.Width), 0, 1);
                await Expect(Page.GetByText(SubmittedMessage, new() { Exact = true })).ToHaveCountAsync(1);
                await Expect(submit).ToBeDisabledAsync();
                await Expect(request.Locator(".reserved-indicator")).ToBeHiddenAsync();
                await Expect(request.Locator("td").Last).ToHaveTextAsync(string.Empty);

                assessment.SetResult(true);
                await Expect(request.GetByRole(AriaRole.Cell, new() { Name = "Approved", Exact = true })).ToBeVisibleAsync();
                await Expect(request.Locator("td").Last).ToHaveTextAsync(string.Empty);
                await Expect(request.Locator(".reserved-indicator")).ToBeHiddenAsync();
                await Expect(submit).ToBeDisabledAsync();

                // The host must finish allocation before the POST claims success.
                allocation.SetResult(true);
                await Expect(request.GetByRole(AriaRole.Cell, new() { Name = "Reserved", Exact = true })).ToBeVisibleAsync();
                await Expect(request.Locator(".reserved-indicator")).ToBeVisibleAsync();
                await Expect(request.Locator("td").Last).ToHaveTextAsync("Prefix or ID Owners Only");
                await Expect(submit).ToBeEnabledAsync();
                await Expect(feedback).ToHaveTextAsync(SubmittedMessage);
                await Expect(feedback.Locator(".alert-brand-success")).ToBeVisibleAsync();
                await Expect(feedback.Locator(".ms-Icon--CompletedSolid")).ToBeVisibleAsync();
            }
            finally
            {
                save.TrySetResult(true);
                assessment.TrySetResult(true);
                allocation.TrySetResult(true);
            }
        }

        [Theory]
        [InlineData(null, false, "", "Unavailable", null)]
        [InlineData("Pending", false, "", "Pending", "Must not display non-rejection details.")]
        [InlineData("Approved", false, "", "Approved", "Must not display non-rejection details.")]
        [InlineData("Rejected", true, "", "Rejected", null)]
        [InlineData("Rejected", false, "", "Rejected", "Provide more evidence. <strong>Details</strong> & links.")]
        [InlineData("Reserved", false, "Prefix or ID Owners Only", "Reserved", "Must not display non-rejection details.")]
        [InlineData("Reserved", true, "Any NuGet.org Account", "Reserved", null)]
        [Priority(1)]
        [Category("P1Tests")]
        [Category("PlaywrightTests")]
        public async Task NamespaceTemplateOnlyShowsRestrictionsForReservedRows(string status, bool isPublic, string restrictions, string displayedStatus, string rejectionReason)
        {
            await SignInAsync();
            await Page.GotoAsync(UrlHelper.ManageMyPackagesUrl);
            var row = new Dictionary<string, object>
            {
                ["Pattern"] = "Contoso.TemplateTest",
                ["SearchUrl"] = "/",
                ["Owners"] = Array.Empty<object>(),
                ["IsPublic"] = isPublic
            };
            if (status != null)
            {
                row["Status"] = status;
            }
            if (rejectionReason != null)
            {
                row["RejectionReason"] = rejectionReason;
            }

            // Exercise the actual Knockout template, including a legacy row with no
            // Status property. Rendering must not throw or imply a reservation.
            await Page.EvaluateAsync(@"row => {
                var host = document.createElement('div');
                host.id = 'namespace-template-regression';
                host.className = 'page-manage-packages';
                document.body.appendChild(host);
                row.Visible = ko.observable(true);
                ko.renderTemplate('manage-namespaces', {
                    Namespaces: [row], VisibleNamespacesCount: ko.observable(1)
                }, {}, host);
            }", row);

            var renderedRow = Page.Locator("#namespace-template-regression .manage-package-listing");
            await Expect(Page.Locator("#namespace-template-regression tbody tr")).ToHaveCountAsync(1);
            var statusLinks = renderedRow.Locator("td").Nth(3).GetByRole(AriaRole.Link);
            if (status == "Rejected")
            {
                var contactUrl = await Page.Locator(".footer-heading")
                    .GetByRole(AriaRole.Link, new() { Name = "Contact", Exact = true }).GetAttributeAsync("href");
                Assert.False(string.IsNullOrEmpty(contactUrl));
                await Expect(statusLinks).ToHaveCountAsync(1);
                await Expect(statusLinks).ToHaveTextAsync("Rejected");
                await Expect(statusLinks).ToHaveAttributeAsync("href", contactUrl);
                var statusCell = renderedRow.Locator("td").Nth(3);
                await Expect(statusCell.Locator("strong")).ToHaveCountAsync(0);
                var reason = statusCell.Locator("p.namespace-rejection-reason");
                await Expect(reason).ToBeVisibleAsync();
                await Expect(reason).ToHaveTextAsync(rejectionReason ?? "No rejection reason was provided.");
                await Expect(reason).ToHaveCSSAsync("white-space", "pre-line");
                var cellBounds = await statusCell.BoundingBoxAsync();
                var reasonBounds = await reason.BoundingBoxAsync();
                Assert.NotNull(cellBounds);
                Assert.NotNull(reasonBounds);
                Assert.True(reasonBounds.X >= cellBounds.X);
                Assert.True(reasonBounds.X + reasonBounds.Width <= cellBounds.X + cellBounds.Width + 1);
                // Customer-facing model text must remain text, never rendered HTML.
                await Expect(reason.Locator("*")).ToHaveCountAsync(0);
            }
            else
            {
                await Expect(renderedRow.Locator("td").Nth(3)).ToHaveTextAsync(displayedStatus);
                await Expect(statusLinks).ToHaveCountAsync(0);
                await Expect(renderedRow.Locator(".namespace-rejection-reason")).ToHaveCountAsync(0);
            }
            await Expect(renderedRow.Locator("td").Last).ToHaveTextAsync(restrictions);
            if (status != "Reserved")
            {
                await Expect(renderedRow.Locator(".reserved-indicator")).ToBeHiddenAsync();
            }
            if (status == "Rejected")
            {
                // Owner filtering must hide the explanation together with its request.
                await Page.EvaluateAsync(@"() => ko.dataFor(document.querySelector(
                    '#namespace-template-regression .manage-package-listing')).Visible(false)");
                await Expect(renderedRow).ToBeHiddenAsync();
                await Expect(renderedRow.Locator(".namespace-rejection-reason")).ToBeHiddenAsync();
            }
        }

        [Fact]
        [Priority(1)]
        [Category("P1Tests")]
        [Category("PlaywrightTests")]
        public async Task ManagePackages_RejectedSubmissionShowsGlobalErrorWithoutInventingPendingRow()
        {
            await SignInAsync();
            await Page.RouteAsync("**/account/NamespaceReservationStatuses*", route => route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Body = "[]"
            }));
            await Page.RouteAsync("**/account/RequestNamespaceReservation", route => route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(new
                {
                    Success = false,
                    Errors = new[] { new { Key = "", Messages = new[] { "Confirm your email address before requesting a namespace reservation." } } }
                })
            }));

            await Page.GotoAsync(UrlHelper.ManageMyPackagesUrl);
            await Page.Locator("#show-namespaces-container").ClickAsync();
            var section = Page.Locator("#namespaces-container");
            await Expect(section.Locator("tbody tr")).ToHaveCountAsync(0);
            await section.GetByLabel("Namespace", new() { Exact = true }).FillAsync("Contoso.ErrorTest");
            await section.GetByLabel("Owners", new() { Exact = true }).FillAsync(GalleryConfiguration.Instance.Account.Name);
            await section.GetByLabel("Justification", new() { Exact = true }).FillAsync("Our packages use this namespace.");
            var submit = section.GetByRole(AriaRole.Button, new() { Name = "Submit request", Exact = true });
            await submit.ClickAsync();

            await Expect(section.Locator("[data-valmsg-summary=true]")).ToContainTextAsync("Confirm your email address");
            await Expect(section.Locator("#namespace-request-feedback .alert-brand-danger")).ToBeVisibleAsync();
            await Expect(section.Locator("#namespace-request-feedback .ms-Icon--ErrorBadge")).ToBeVisibleAsync();
            await Expect(section.Locator("#namespace-request-feedback")).Not.ToContainTextAsync(SubmittedMessage);
            await Expect(section.Locator("tbody tr")).ToHaveCountAsync(0);
            await Expect(section.GetByLabel("Namespace", new() { Exact = true })).ToHaveValueAsync("Contoso.ErrorTest");
            await Expect(submit).ToBeEnabledAsync();
        }

        [Fact]
        [Priority(1)]
        [Category("P1Tests")]
        [Category("PlaywrightTests")]
        public async Task ManagePackages_UncertainAllocationShowsBottomWarningInsteadOfSuccess()
        {
            await SignInAsync();
            const string warning = "Your request was approved, but automatic namespace reservation could not be confirmed. Check the table or contact support before retrying.";
            await Page.RouteAsync("**/account/NamespaceReservationStatuses*", route => route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Body = "[]"
            }));
            await Page.RouteAsync("**/account/RequestNamespaceReservation", route => route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(new { Success = true, Message = warning, IsWarning = true })
            }));

            await Page.GotoAsync(UrlHelper.ManageMyPackagesUrl);
            await Page.Locator("#show-namespaces-container").ClickAsync();
            var section = Page.Locator("#namespaces-container");
            await section.GetByLabel("Namespace", new() { Exact = true }).FillAsync("Contoso.WarningTest");
            await section.GetByLabel("Owners", new() { Exact = true }).FillAsync(GalleryConfiguration.Instance.Account.Name);
            await section.GetByLabel("Justification", new() { Exact = true }).FillAsync("Our packages use this namespace.");
            await section.GetByRole(AriaRole.Button, new() { Name = "Submit request", Exact = true }).ClickAsync();

            var feedback = section.Locator("#namespace-request-form > fieldset + #namespace-request-feedback");
            await Expect(feedback).ToBeVisibleAsync();
            await Expect(feedback).ToHaveTextAsync(warning);
            await Expect(feedback.Locator(".alert-brand-warning")).ToBeVisibleAsync();
            await Expect(feedback.Locator(".ms-Icon--Warning")).ToBeVisibleAsync();
            await Expect(feedback.Locator(".alert-brand-success")).ToBeHiddenAsync();
            await Expect(section.Locator("tbody tr")).ToHaveCountAsync(0);
        }
    }
}