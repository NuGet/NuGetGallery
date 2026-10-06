// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.Filters;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class NamespaceReservationRequestControllerFacts : TestContainer
    {
        private readonly User _user;
        private readonly UsersController _controller;
        private readonly NamespaceReservationRequestInput _input;

        public NamespaceReservationRequestControllerFacts()
        {
            _user = Get<Fakes>().CreateUser("requester");
            _controller = GetController<UsersController>();
            _controller.SetCurrentUser(_user);
            // IsAjaxRequest checks both the request indexer and Headers. Keep ordinary
            // requests explicitly non-AJAX without replacing TestContainer's context.
            GetMock<HttpContextBase>().Setup(c => c.Request["X-Requested-With"]).Returns((string)null);
            GetMock<HttpContextBase>().Setup(c => c.Request.Headers).Returns(new NameValueCollection());
            _input = new NamespaceReservationRequestInput
            {
                Namespace = "Contoso",
                Owner = _user.Username,
                Justification = "Our packages use this namespace."
            };
            GetMock<INamespaceReservationRequestService>()
                .Setup(s => s.SubmitAsync(_user, It.IsAny<NamespaceReservationRequestInput>()))
                .ReturnsAsync(new NamespaceReservationSubmissionResult(Array.Empty<ValidationResult>(),
                    "Your namespace reservation request was saved with status Pending. Model assessment is disabled. No namespace has been reserved."));
        }

        [Fact]
        public async Task SavesUsingAuthenticatedUserAndRedirectsAfterSuccess()
        {
            var result = await _controller.RequestNamespaceReservation(_input);

            var redirect = Assert.IsType<RedirectToRouteResult>(result);
            Assert.Equal("Packages", redirect.RouteValues["action"]);
            Assert.Equal("Your request has been submitted successfully. It is being processed. Please check back later.", NamespaceReservationSubmissionResult.SubmittedMessage);
            Assert.Equal(NamespaceReservationSubmissionResult.SubmittedMessage, Assert.IsType<string>(_controller.TempData["NamespaceRequestMessage"]));
            Assert.False(Assert.IsType<bool>(_controller.TempData["NamespaceRequestWarning"]));
            Assert.False(_controller.TempData.ContainsKey("Message"));
            GetMock<INamespaceReservationRequestService>().Verify(s => s.SubmitAsync(_user, _input), Times.Once);
        }

        [Theory]
        [InlineData("Approved")]
        [InlineData("Rejected")]
        [InlineData("Pending")]
        public async Task UsesGenericConfirmationInsteadOfServiceStatusAndReason(string status)
        {
            var message = "Your namespace reservation request was saved with status " + status
                + ". Customer reason <script>alert('untrusted')</script> & details. No namespace has been reserved.";
            GetMock<INamespaceReservationRequestService>()
                .Setup(s => s.SubmitAsync(_user, _input))
                .ReturnsAsync(new NamespaceReservationSubmissionResult(Array.Empty<ValidationResult>(), message));

            var result = await _controller.RequestNamespaceReservation(_input);

            Assert.Equal("Packages", Assert.IsType<RedirectToRouteResult>(result).RouteValues["action"]);
            Assert.Equal(NamespaceReservationSubmissionResult.SubmittedMessage, Assert.IsType<string>(_controller.TempData["NamespaceRequestMessage"]));
            Assert.False(Assert.IsType<bool>(_controller.TempData["NamespaceRequestWarning"]));
            Assert.False(_controller.TempData.ContainsKey("Message"));
            Assert.Null(_controller.TempData["RawErrorMessage"]);
            Assert.True(_controller.ModelState.IsValid);
            GetMock<INamespaceReservationRequestService>().Verify(s => s.SubmitAsync(_user, _input), Times.Once);
        }

        [Theory]
        [InlineData("Your namespace reservation request was saved, but we could not confirm that the assessment decision was saved. No namespace has been reserved.")]
        [InlineData("Your request was approved, but automatic namespace reservation could not be confirmed. Check the table or contact support before retrying.")]
        public async Task UncertainOutcomeRedirectsWithServiceWarningWithoutRetryOrReplacingItWithSuccess(string message)
        {
            GetMock<INamespaceReservationRequestService>()
                .Setup(s => s.SubmitAsync(_user, _input))
                .ReturnsAsync(new NamespaceReservationSubmissionResult(Array.Empty<ValidationResult>(), message, isWarning: true));

            var result = await _controller.RequestNamespaceReservation(_input);

            Assert.Equal("Packages", Assert.IsType<RedirectToRouteResult>(result).RouteValues["action"]);
            Assert.Equal(message, Assert.IsType<string>(_controller.TempData["NamespaceRequestMessage"]));
            Assert.True(Assert.IsType<bool>(_controller.TempData["NamespaceRequestWarning"]));
            Assert.False(_controller.TempData.ContainsKey("Message"));
            GetMock<INamespaceReservationRequestService>().Verify(s => s.SubmitAsync(_user, _input), Times.Once);
        }

        [Fact]
        public async Task ValidationErrorsPreserveInputsAndExpandSection()
        {
            GetMock<INamespaceReservationRequestService>()
                .Setup(s => s.SubmitAsync(_user, _input))
                .ReturnsAsync(new NamespaceReservationSubmissionResult(
                    new[] { new ValidationResult("Owner is not allowed.", new[] { "Owner" }), new ValidationResult("Confirm your email.") },
                    "Must not display a success message when there are validation errors."));

            var result = await _controller.RequestNamespaceReservation(_input);

            AssertFailedView(result);
            Assert.Equal("Owner is not allowed.", Assert.Single(_controller.ModelState["NamespaceRequest.Owner"].Errors).ErrorMessage);
            Assert.Equal("Confirm your email.", Assert.Single(_controller.ModelState[string.Empty].Errors).ErrorMessage);
            Assert.Null(_controller.TempData["Message"]);
        }

        [Fact]
        public async Task InvalidModelStateDoesNotSave()
        {
            _controller.ModelState.AddModelError("NamespaceRequest.Justification", "Enter a justification.");

            var result = await _controller.RequestNamespaceReservation(_input);

            AssertFailedView(result);
            GetMock<INamespaceReservationRequestService>().Verify(s => s.SubmitAsync(It.IsAny<User>(), It.IsAny<NamespaceReservationRequestInput>()), Times.Never);
        }

        [Fact]
        public async Task ReadOnlyModeDoesNotSave()
        {
            ((AppConfiguration)Get<IAppConfiguration>()).ReadOnlyMode = true;

            var result = await _controller.RequestNamespaceReservation(_input);

            AssertFailedView(result);
            Assert.Contains("read-only", Assert.Single(_controller.ModelState[string.Empty].Errors).ErrorMessage);
            GetMock<INamespaceReservationRequestService>().Verify(s => s.SubmitAsync(It.IsAny<User>(), It.IsAny<NamespaceReservationRequestInput>()), Times.Never);
        }

        [Fact]
        public async Task DatabaseFailureShowsSafeErrorInsteadOfSuccess()
        {
            var exception = new DbUpdateException("Private SQL details");
            GetMock<INamespaceReservationRequestService>()
                .Setup(s => s.SubmitAsync(_user, _input))
                .ThrowsAsync(exception);

            var result = await _controller.RequestNamespaceReservation(_input);

            AssertFailedView(result);
            var message = Assert.Single(_controller.ModelState[string.Empty].Errors).ErrorMessage;
            Assert.Contains("could not confirm", message);
            Assert.DoesNotContain("Private SQL", message);
            Assert.Null(_controller.TempData["Message"]);
            GetMock<ITelemetryService>().Verify(t => t.TrackException(exception, It.IsAny<Action<Dictionary<string, string>>>()), Times.Once);
        }

        [Fact]
        public void PostRequiresAuthenticationAndAntiForgery()
        {
            var action = typeof(UsersController).GetMethod(nameof(UsersController.RequestNamespaceReservation));
            Assert.True(Attribute.IsDefined(action, typeof(HttpPostAttribute)));
            Assert.True(Attribute.IsDefined(action, typeof(UIAuthorizeAttribute)));
            Assert.True(Attribute.IsDefined(action, typeof(ValidateAntiForgeryTokenAttribute)));
        }

        [Fact]
        public void PackagesLeavesRequestFieldsEmptyAndKeepsSectionCollapsed()
        {
            var model = ResultAssert.IsView<ManagePackagesViewModel>(_controller.Packages());

            Assert.Null(model.NamespaceRequest.Namespace);
            Assert.Null(model.NamespaceRequest.Owner);
            Assert.Null(model.NamespaceRequest.Justification);
            Assert.Null(model.NamespaceRequestMessage);
            Assert.False(model.NamespaceRequestWarning);
            Assert.False(model.ExpandNamespaceRequest);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PackagesConsumesNamespaceMessageAndWarningAndExpandsSection(bool isWarning)
        {
            var message = isWarning ? "The assessment decision save could not be confirmed." : NamespaceReservationSubmissionResult.SubmittedMessage;
            _controller.TempData["NamespaceRequestMessage"] = message;
            _controller.TempData["NamespaceRequestWarning"] = isWarning;
            _controller.TempData["Message"] = "Unrelated global message.";

            var model = ResultAssert.IsView<ManagePackagesViewModel>(_controller.Packages());

            Assert.Equal(message, model.NamespaceRequestMessage);
            Assert.Equal(isWarning, model.NamespaceRequestWarning);
            Assert.True(model.ExpandNamespaceRequest);
            Assert.Equal("Unrelated global message.", _controller.TempData.Peek("Message"));
            // Saving TempData at the end of the request removes consumed entries.
            _controller.TempData.Save(_controller.ControllerContext, new Mock<ITempDataProvider>().Object);
            Assert.False(_controller.TempData.ContainsKey("NamespaceRequestMessage"));
            Assert.False(_controller.TempData.ContainsKey("NamespaceRequestWarning"));
            Assert.Equal("Unrelated global message.", _controller.TempData.Peek("Message"));

            var nextModel = ResultAssert.IsView<ManagePackagesViewModel>(_controller.Packages());
            Assert.Null(nextModel.NamespaceRequestMessage);
            Assert.False(nextModel.NamespaceRequestWarning);
            Assert.False(nextModel.ExpandNamespaceRequest);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SubmissionPreservesUnrelatedGlobalMessage(bool ajax)
        {
            if (ajax)
            {
                SetupAjaxRequest();
            }

            _controller.TempData["Message"] = "Unrelated global message.";

            await _controller.RequestNamespaceReservation(_input);

            Assert.Equal("Unrelated global message.", _controller.TempData.Peek("Message"));
        }

        [Fact]
        public void PackagesCombinesSubmittedRequestsWithUserAndOrganizationReservations()
        {
            var organization = SetupNamespaceRows();
            SetupExternallySelectedUser(organization.Key);

            var model = ResultAssert.IsView<ManagePackagesViewModel>(_controller.Packages());

            Assert.Same(_user, model.User);
            var rows = model.ReservedNamespaces.ReservedNamespaces.ToArray();
            Assert.Equal(5, rows.Length);
            Assert.Equal(new[] { "Reserved", "Reserved", "Pending", "Approved", "Rejected" }, rows.Select(row => row.Status));
            foreach (var status in new[] { "Pending", "Approved", "Rejected" })
            {
                var request = Assert.Single(rows.Where(row => row.Value == "Requested" + status));
                Assert.Equal(status, request.Status);
                Assert.Equal(status == "Rejected" ? "Provide more evidence of your connection to this namespace." : null, request.RejectionReason);
                Assert.False(request.IsPublic);
                Assert.False(request.IsPrefix);
                Assert.Collection(request.Owners,
                    owner => Assert.Same(_user, owner),
                    owner => Assert.Same(organization, owner));
            }

            var userReservation = Assert.Single(rows.Where(row => row.Value == "UserReserved"));
            Assert.Equal("Reserved", userReservation.Status);
            Assert.False(userReservation.IsPublic);
            Assert.Same(_user, Assert.Single(userReservation.Owners));
            var organizationReservation = Assert.Single(rows.Where(row => row.Value == "OrganizationReserved."));
            Assert.Equal("Reserved", organizationReservation.Status);
            Assert.Equal("OrganizationReserved.*", organizationReservation.GetPattern());
            Assert.True(organizationReservation.IsPublic);
            Assert.Same(organization, Assert.Single(organizationReservation.Owners));
            VerifyRequestsUseAuthenticatedUser();
        }

        [Fact]
        public void StatusGetRequiresAuthenticationHasNoArgumentsAndDisablesCaching()
        {
            var action = typeof(UsersController).GetMethod(nameof(UsersController.NamespaceReservationStatuses));

            Assert.NotNull(action);
            Assert.Empty(action.GetParameters());
            Assert.True(Attribute.IsDefined(action, typeof(HttpGetAttribute)));
            Assert.True(Attribute.IsDefined(action, typeof(UIAuthorizeAttribute)));
            var cache = Assert.IsType<OutputCacheAttribute>(Attribute.GetCustomAttribute(action, typeof(OutputCacheAttribute)));
            Assert.True(cache.NoStore);
            Assert.Equal(0, cache.Duration);
        }

        [Fact]
        public void StatusGetUsesAuthenticatedUserAndExposesOnlyDisplayFieldsIncludingRejectionReason()
        {
            var organization = SetupNamespaceRows();
            SetupExternallySelectedUser(organization.Key);
            _controller.Url = TestUtility.MockUrlHelper();

            var result = Assert.IsType<JsonResult>(_controller.NamespaceReservationStatuses());

            Assert.Equal(JsonRequestBehavior.AllowGet, result.JsonRequestBehavior);
            var rows = JArray.FromObject(result.Data);
            Assert.Equal(5, rows.Count);
            Assert.Equal(new[] { "Reserved", "Reserved", "Pending", "Approved", "Rejected" }, rows.Select(row => row.Value<string>("Status")));
            foreach (var row in rows.Cast<JObject>())
            {
                Assert.Equal(new[] { "IsPublic", "Owners", "Pattern", "RejectionReason", "SearchUrl", "Status" },
                    row.Properties().Select(property => property.Name).OrderBy(name => name).ToArray());
                Assert.Equal(row.Value<string>("Status") == "Rejected"
                    ? "Provide more evidence of your connection to this namespace." : null, row.Value<string>("RejectionReason"));
                foreach (var owner in Assert.IsType<JArray>(row["Owners"]).Cast<JObject>())
                {
                    Assert.Equal(new[] { "IsOrganization", "ProfileUrl", "Username" },
                        owner.Properties().Select(property => property.Name).OrderBy(name => name).ToArray());
                    var isOrganization = owner.Value<string>("Username") == organization.Username;
                    Assert.Equal(isOrganization, owner.Value<bool>("IsOrganization"));
                    Assert.Equal(_controller.Url.User(isOrganization ? organization : _user), owner.Value<string>("ProfileUrl"));
                }
            }

            foreach (var status in new[] { "Pending", "Approved", "Rejected" })
            {
                var row = Assert.Single(rows.Where(item => item.Value<string>("Pattern") == "Requested" + status));
                Assert.Equal(status, row.Value<string>("Status"));
                Assert.False(row.Value<bool>("IsPublic"));
                Assert.Equal(_controller.Url.Search("Requested" + status), row.Value<string>("SearchUrl"));
                Assert.Equal(new[] { _user.Username, organization.Username },
                    row["Owners"].Select(owner => owner.Value<string>("Username")).ToArray());
            }

            var userRow = Assert.Single(rows.Where(row => row.Value<string>("Pattern") == "UserReserved"));
            Assert.Equal("Reserved", userRow.Value<string>("Status"));
            Assert.False(userRow.Value<bool>("IsPublic"));
            Assert.Equal(_controller.Url.Search("UserReserved"), userRow.Value<string>("SearchUrl"));
            Assert.Equal(_user.Username, Assert.Single(userRow["Owners"]).Value<string>("Username"));
            var organizationRow = Assert.Single(rows.Where(row => row.Value<string>("Pattern") == "OrganizationReserved.*"));
            Assert.Equal("Reserved", organizationRow.Value<string>("Status"));
            Assert.True(organizationRow.Value<bool>("IsPublic"));
            Assert.Equal(_controller.Url.Search("OrganizationReserved."), organizationRow.Value<string>("SearchUrl"));
            Assert.Equal(organization.Username, Assert.Single(organizationRow["Owners"]).Value<string>("Username"));
            Assert.DoesNotContain(rows.Descendants().OfType<JProperty>(), property =>
                property.Name == "Reason" || property.Name == "Reasons" || property.Name == "Justification"
                || property.Name.StartsWith("SubmittedBy", StringComparison.Ordinal) || property.Name == "Key");
            Assert.DoesNotContain("Private assessment details", rows.ToString());
            Assert.DoesNotContain("Private justification", rows.ToString());
            VerifyRequestsUseAuthenticatedUser();
        }

        [Fact]
        public void StatusGetReturnsEmptyArrayForCurrentUserWithoutNamespaces()
        {
            SetupNamespaceRows();
            var currentUser = new User("another-requester") { Key = 99 };
            _controller.SetCurrentUser(currentUser);
            SetupExternallySelectedUser(_user.Key);
            GetMock<INamespaceReservationRequestService>()
                .Setup(s => s.GetRequestsForUser(currentUser))
                .Returns(Array.Empty<NamespaceReservationRequest>());
            _controller.Url = TestUtility.MockUrlHelper();

            var result = Assert.IsType<JsonResult>(_controller.NamespaceReservationStatuses());

            Assert.Equal(JsonRequestBehavior.AllowGet, result.JsonRequestBehavior);
            Assert.Empty(JArray.FromObject(result.Data));
            GetMock<INamespaceReservationRequestService>().Verify(s => s.GetRequestsForUser(
                It.Is<User>(user => ReferenceEquals(user, currentUser))), Times.Once);
            GetMock<INamespaceReservationRequestService>().Verify(s => s.GetRequestsForUser(_user), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AjaxSuccessUsesGenericConfirmationWithoutTempDataOrRedirect(bool useRequestIndexer)
        {
            SetupAjaxRequest(useRequestIndexer);
            const string message = "Approved (assessment only). Customer reason <script>alert('untrusted')</script> & details. No namespace has been reserved.";
            GetMock<INamespaceReservationRequestService>()
                .Setup(s => s.SubmitAsync(_user, _input))
                .ReturnsAsync(new NamespaceReservationSubmissionResult(Array.Empty<ValidationResult>(), message));

            var result = await _controller.RequestNamespaceReservation(_input);

            var data = AssertAjaxResult(result, success: true);
            Assert.Equal(new[] { "IsWarning", "Message", "Success" }, data.Properties().Select(property => property.Name).OrderBy(name => name).ToArray());
            Assert.Equal(NamespaceReservationSubmissionResult.SubmittedMessage, data.Value<string>("Message"));
            Assert.False(data.Value<bool>("IsWarning"));
            Assert.True(_controller.ModelState.IsValid);
            GetMock<INamespaceReservationRequestService>().Verify(s => s.SubmitAsync(_user, _input), Times.Once);
        }

        [Fact]
        public async Task AjaxValidationReturnsKeyedErrorsWithoutSuccessMessage()
        {
            SetupAjaxRequest();
            GetMock<INamespaceReservationRequestService>()
                .Setup(s => s.SubmitAsync(_user, _input))
                .ReturnsAsync(new NamespaceReservationSubmissionResult(new[]
                {
                    new ValidationResult("Owner is not allowed.", new[] { "Owner" }),
                    new ValidationResult("Check namespace and owner.", new[] { "Namespace", "Owner" }),
                    new ValidationResult("Confirm your email.")
                }, "Must not display a success message."));

            var result = await _controller.RequestNamespaceReservation(_input);

            var errors = AssertAjaxErrors(result);
            Assert.Equal(3, errors.Count);
            Assert.Equal(new[] { "Owner is not allowed.", "Check namespace and owner." }, errors["NamespaceRequest.Owner"]);
            Assert.Equal(new[] { "Check namespace and owner." }, errors["NamespaceRequest.Namespace"]);
            Assert.Equal(new[] { "Confirm your email." }, errors[string.Empty]);
            GetMock<INamespaceReservationRequestService>().Verify(s => s.SubmitAsync(_user, _input), Times.Once);
        }

        [Fact]
        public async Task AjaxInvalidModelStateHidesExceptionDetailsAndDoesNotSave()
        {
            SetupAjaxRequest();
            _controller.ModelState.AddModelError("NamespaceRequest.Justification", "Enter a justification.");
            _controller.ModelState.AddModelError("NamespaceRequest.Owner", new InvalidOperationException("Private exception details"));
            _controller.ModelState.SetModelValue("NamespaceRequest.Namespace", new ValueProviderResult("Contoso", "Contoso", null));

            var result = await _controller.RequestNamespaceReservation(_input);

            var errors = AssertAjaxErrors(result);
            Assert.Equal(2, errors.Count);
            Assert.Equal(new[] { "Enter a justification." }, errors["NamespaceRequest.Justification"]);
            Assert.Equal(new[] { "The supplied value is invalid." }, errors["NamespaceRequest.Owner"]);
            Assert.DoesNotContain("Private exception details", JObject.FromObject(Assert.IsType<JsonResult>(result).Data).ToString());
            GetMock<INamespaceReservationRequestService>().Verify(s => s.SubmitAsync(It.IsAny<User>(), It.IsAny<NamespaceReservationRequestInput>()), Times.Never);
        }

        [Fact]
        public async Task AjaxReadOnlyModeReturnsSafeErrorAndDoesNotSave()
        {
            SetupAjaxRequest();
            ((AppConfiguration)Get<IAppConfiguration>()).ReadOnlyMode = true;

            var result = await _controller.RequestNamespaceReservation(_input);

            var errors = AssertAjaxErrors(result);
            Assert.Single(errors);
            Assert.Equal("Namespace requests cannot be saved while the Gallery is in read-only mode. Please try again later.",
                Assert.Single(errors[string.Empty]));
            GetMock<INamespaceReservationRequestService>().Verify(s => s.SubmitAsync(It.IsAny<User>(), It.IsAny<NamespaceReservationRequestInput>()), Times.Never);
        }

        [Fact]
        public async Task AjaxDatabaseFailureReturnsSafeAmbiguousSaveErrorWithoutRetry()
        {
            SetupAjaxRequest();
            var exception = new DbUpdateException("Private SQL details");
            GetMock<INamespaceReservationRequestService>()
                .Setup(s => s.SubmitAsync(_user, _input))
                .ThrowsAsync(exception);

            var result = await _controller.RequestNamespaceReservation(_input);

            var errors = AssertAjaxErrors(result);
            Assert.Single(errors);
            Assert.Equal("We could not confirm that your namespace request was saved. Please try again later.",
                Assert.Single(errors[string.Empty]));
            Assert.DoesNotContain("Private SQL details", JObject.FromObject(Assert.IsType<JsonResult>(result).Data).ToString());
            GetMock<INamespaceReservationRequestService>().Verify(s => s.SubmitAsync(_user, _input), Times.Once);
            GetMock<ITelemetryService>().Verify(t => t.TrackException(exception, It.IsAny<Action<Dictionary<string, string>>>()), Times.Once);
        }

        [Theory]
        [InlineData("Your namespace reservation request was saved, but we could not confirm that the assessment decision was saved. No namespace has been reserved.")]
        [InlineData("Your request was approved, but automatic namespace reservation could not be confirmed. Check the table or contact support before retrying.")]
        public async Task AjaxUncertainOutcomePreservesServiceWarningWithoutRetry(string message)
        {
            SetupAjaxRequest();
            GetMock<INamespaceReservationRequestService>()
                .Setup(s => s.SubmitAsync(_user, _input))
                .ReturnsAsync(new NamespaceReservationSubmissionResult(Array.Empty<ValidationResult>(), message, isWarning: true));

            var result = await _controller.RequestNamespaceReservation(_input);

            var data = AssertAjaxResult(result, success: true);
            Assert.Equal(new[] { "IsWarning", "Message", "Success" }, data.Properties().Select(property => property.Name).OrderBy(name => name).ToArray());
            Assert.Equal(message, data.Value<string>("Message"));
            Assert.True(data.Value<bool>("IsWarning"));
            Assert.Null(data["Errors"]);
            Assert.True(_controller.ModelState.IsValid);
            GetMock<INamespaceReservationRequestService>().Verify(s => s.SubmitAsync(_user, _input), Times.Once);
        }

        private Organization SetupNamespaceRows()
        {
            _user.Key = 10;
            var organization = new Organization("requester-organization") { Key = 20 };
            var membership = new Membership
            {
                Member = _user,
                MemberKey = _user.Key,
                Organization = organization,
                OrganizationKey = organization.Key,
                IsAdmin = true
            };
            _user.Organizations.Add(membership);
            organization.Members.Add(membership);
            var userReservation = new ReservedNamespace("UserReserved", isSharedNamespace: false, isPrefix: false);
            userReservation.Owners.Add(_user);
            _user.ReservedNamespaces.Add(userReservation);
            var organizationReservation = new ReservedNamespace("OrganizationReserved.", isSharedNamespace: true, isPrefix: true);
            organizationReservation.Owners.Add(organization);
            organization.ReservedNamespaces.Add(organizationReservation);
            var requests = new[] { "Pending", "Approved", "Rejected" }.Select(status => new NamespaceReservationRequest
            {
                Namespace = "Requested" + status,
                Status = status,
                SubmittedByUser = _user,
                SubmittedByUserKey = _user.Key,
                Justification = "Private justification",
                Reason = status == "Rejected" ? "Provide more evidence of your connection to this namespace." : "Private assessment details",
                RequestedOwnersJson = JArray.FromObject(new[]
                {
                    new { _user.Key, Username = "previous-user-name" },
                    new { organization.Key, Username = "previous-organization-name" }
                }).ToString()
            }).ToArray();
            GetMock<INamespaceReservationRequestService>()
                .Setup(s => s.GetRequestsForUser(_user))
                .Returns(requests);
            return organization;
        }

        private void SetupExternallySelectedUser(int userKey)
        {
            GetMock<HttpContextBase>().Setup(c => c.Request.QueryString).Returns(new NameValueCollection
            {
                { "userKey", userKey.ToString() },
                { "owner", "externally-selected-owner" }
            });
            _controller.RouteData.Values["userKey"] = userKey;
        }

        private void VerifyRequestsUseAuthenticatedUser()
        {
            GetMock<INamespaceReservationRequestService>().Verify(s => s.GetRequestsForUser(
                It.Is<User>(user => ReferenceEquals(user, _user))), Times.Once);
            GetMock<INamespaceReservationRequestService>().Verify(s => s.GetRequestsForUser(It.IsAny<User>()), Times.Once);
        }

        private void SetupAjaxRequest(bool useRequestIndexer = false)
        {
            if (useRequestIndexer)
            {
                GetMock<HttpContextBase>().Setup(c => c.Request["X-Requested-With"]).Returns("XMLHttpRequest");
            }
            else
            {
                GetMock<HttpContextBase>().Setup(c => c.Request.Headers).Returns(new NameValueCollection
                {
                    { "X-Requested-With", "XMLHttpRequest" }
                });
            }
        }

        private JObject AssertAjaxResult(ActionResult result, bool success)
        {
            var json = Assert.IsType<JsonResult>(result);
            Assert.Equal(JsonRequestBehavior.DenyGet, json.JsonRequestBehavior);
            var data = JObject.FromObject(json.Data);
            Assert.Equal(success, data.Value<bool>("Success"));
            Assert.Empty(_controller.TempData);
            GetMock<INamespaceReservationRequestService>().Verify(s => s.GetRequestsForUser(It.IsAny<User>()), Times.Never);
            return data;
        }

        private Dictionary<string, string[]> AssertAjaxErrors(ActionResult result)
        {
            var data = AssertAjaxResult(result, success: false);
            Assert.Equal(new[] { "Errors", "Success" }, data.Properties().Select(property => property.Name).OrderBy(name => name).ToArray());
            Assert.False(_controller.ModelState.IsValid);
            var errors = Assert.IsType<JArray>(data["Errors"]);
            Assert.All(errors.Cast<JObject>(), error => Assert.Equal(new[] { "Key", "Messages" },
                error.Properties().Select(property => property.Name).OrderBy(name => name).ToArray()));
            return errors.ToDictionary(error => error.Value<string>("Key"),
                error => Assert.IsType<JArray>(error["Messages"]).Values<string>().ToArray());
        }

        private void AssertFailedView(ActionResult result)
        {
            var model = ResultAssert.IsView<ManagePackagesViewModel>(result, "Packages");
            Assert.Same(_input, model.NamespaceRequest);
            Assert.True(model.ExpandNamespaceRequest);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Null(model.NamespaceRequestMessage);
            Assert.False(model.NamespaceRequestWarning);
            Assert.False(_controller.TempData.ContainsKey("NamespaceRequestMessage"));
            Assert.False(_controller.TempData.ContainsKey("NamespaceRequestWarning"));
        }
    }
}