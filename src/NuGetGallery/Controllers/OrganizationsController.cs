// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;
using NuGetGallery.Helpers;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Security;

namespace NuGetGallery
{
    public class OrganizationsController
        : AccountsController<Organization, OrganizationAccountViewModel>
    {
        private readonly IFeatureFlagService _features;

        public OrganizationsController(
            AuthenticationService authService,
            IMessageService messageService,
            IUserService userService,
            ITelemetryService telemetryService,
            ISecurityPolicyService securityPolicyService,
            ICertificateService certificateService,
            IPackageService packageService,
            IDeleteAccountService deleteAccountService,
            IContentObjectService contentObjectService,
            IMessageServiceConfiguration messageServiceConfiguration,
            IIconUrlProvider iconUrlProvider,
            IFeatureFlagService features,
            IGravatarProxyService gravatarProxy)
            : base(
                  authService,
                  packageService,
                  messageService,
                  userService,
                  telemetryService,
                  securityPolicyService,
                  certificateService,
                  contentObjectService,
                  messageServiceConfiguration,
                  deleteAccountService,
                  iconUrlProvider,
                  gravatarProxy)
        {
            _features = features ?? throw new ArgumentNullException(nameof(features));
        }

        public override string AccountAction => nameof(ManageOrganization);

        protected internal override ViewMessages Messages => new ViewMessages
        {
            EmailConfirmed = Strings.OrganizationEmailConfirmed,
            EmailPreferencesUpdated = Strings.OrganizationEmailPreferencesUpdated,
            EmailUpdateCancelled = Strings.OrganizationEmailUpdateCancelled
        };

        protected override Task SendNewAccountEmailAsync(User account)
        {
            var message = new NewAccountMessage(
                MessageServiceConfiguration,
                account,
                Url.ConfirmOrganizationEmail(account.Username, account.EmailConfirmationToken, relativeUrl: false));

            return MessageService.SendMessageAsync(message);
        }

        protected override Task SendEmailChangedConfirmationNoticeAsync(User account)
        {
            var message = new EmailChangeConfirmationMessage(
                MessageServiceConfiguration,
                account,
                Url.ConfirmOrganizationEmail(account.Username, account.EmailConfirmationToken, relativeUrl: false));

            return MessageService.SendMessageAsync(message);
        }

        [HttpGet]
        [UIAuthorize]
        public ActionResult Add()
        {
            return View(new AddOrganizationViewModel());
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Add(AddOrganizationViewModel model)
        {
            var organizationName = model.OrganizationName;
            var organizationEmailAddress = model.OrganizationEmailAddress;
            var adminUser = GetCurrentUser();

            try
            {
                var organization = await UserService.AddOrganizationAsync(organizationName, organizationEmailAddress, adminUser);
                await SendNewAccountEmailAsync(organization);
                TelemetryService.TrackOrganizationAdded(organization);
                return RedirectToAction(nameof(ManageOrganization), new { accountName = organization.Username });
            }
            catch (EntityException e)
            {
                TempData["AddOrganizationErrorMessage"] = e.Message;
                return View(model);
            }
        }

        [HttpGet]
        [UIAuthorize]
        public virtual ActionResult ManageOrganization(string accountName)
        {
            var account = GetAccount(accountName);

            return AccountView(account);
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AddMember(string accountName, string memberName, bool isAdmin)
        {
            var account = GetAccount(accountName);

            if (account == null
                || ActionsRequiringPermissions.ManageMembership.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return Json(HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            if (!account.Confirmed)
            {
                return Json(HttpStatusCode.BadRequest, Strings.Member_OrganizationUnconfirmed);
            }

            try
            {
                var request = await UserService.AddMembershipRequestAsync(account, memberName, isAdmin);
                var currentUser = GetCurrentUser();

                var organizationMembershipRequestMessage = new OrganizationMembershipRequestMessage(
                    MessageServiceConfiguration,
                    account,
                    request.NewMember,
                    currentUser,
                    request.IsAdmin,
                    profileUrl: Url.User(account, relativeUrl: false),
                    confirmationUrl: Url.AcceptOrganizationMembershipRequest(request, relativeUrl: false),
                    rejectionUrl: Url.RejectOrganizationMembershipRequest(request, relativeUrl: false));
                await MessageService.SendMessageAsync(organizationMembershipRequestMessage);

                var organizationMembershipRequestInitiatedMessage = new OrganizationMembershipRequestInitiatedMessage(
                    MessageServiceConfiguration,
                    account,
                    currentUser,
                    request.NewMember,
                    request.IsAdmin);
                await MessageService.SendMessageAsync(organizationMembershipRequestInitiatedMessage);

                return Json(new OrganizationMemberViewModel(request, GetGravatarUrl(request.NewMember)));
            }
            catch (EntityException e)
            {
                return Json(HttpStatusCode.BadRequest, e.Message);
            }
        }

        [HttpGet]
        [UIAuthorize]
        public async Task<ActionResult> ConfirmMemberRequestRedirect(string accountName, string confirmationToken)
        {
            return await ConfirmMemberRequestAsync(accountName, confirmationToken, redirect: true);
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ConfirmMemberRequest(string accountName, string confirmationToken)
        {
            return await ConfirmMemberRequestAsync(accountName, confirmationToken, redirect: false);
        }

        private async Task<ActionResult> ConfirmMemberRequestAsync(string accountName, string confirmationToken, bool redirect)
        {
            var account = GetAccount(accountName);

            if (account == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            if (redirect)
            {
                return Redirect(Url.ManageMyOrganizations());
            }

            try
            {
                var member = await UserService.AddMemberAsync(account, GetCurrentUser().Username, confirmationToken);
                var emailMessage = new OrganizationMemberUpdatedMessage(MessageServiceConfiguration, account, member);
                await MessageService.SendMessageAsync(emailMessage);

                TempData["Message"] = String.Format(CultureInfo.CurrentCulture,
                    Strings.AddMember_Success, account.Username);

                return Redirect(Url.ManageMyOrganization(account.Username));
            }
            catch (EntityException e)
            {
                var failureReason = e.AsUserSafeException().GetUserSafeMessage();
                return HandleOrganizationMembershipRequestView(new HandleOrganizationMembershipRequestModel(true, account, failureReason));
            }
        }

        [HttpGet]
        [UIAuthorize]
        public async Task<ActionResult> RejectMemberRequestRedirect(string accountName, string confirmationToken)
        {
            return await RejectMemberRequestAsync(accountName, confirmationToken, redirect: true);
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RejectMemberRequest(string accountName, string confirmationToken)
        {
            return await RejectMemberRequestAsync(accountName, confirmationToken, redirect: false);
        }

        private async Task<ActionResult> RejectMemberRequestAsync(string accountName, string confirmationToken, bool redirect)
        {
            var account = GetAccount(accountName);

            if (account == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            if (redirect)
            {
                return Redirect(Url.ManageMyOrganizations());
            }

            try
            {
                var member = GetCurrentUser();
                await UserService.RejectMembershipRequestAsync(account, member.Username, confirmationToken);

                var emailMessage = new OrganizationMembershipRequestDeclinedMessage(MessageServiceConfiguration, account, member);
                await MessageService.SendMessageAsync(emailMessage);

                return HandleOrganizationMembershipRequestView(new HandleOrganizationMembershipRequestModel(false, account));
            }
            catch (EntityException e)
            {
                var failureReason = e.AsUserSafeException().GetUserSafeMessage();
                return HandleOrganizationMembershipRequestView(new HandleOrganizationMembershipRequestModel(false, account, failureReason));
            }
        }

        private ActionResult HandleOrganizationMembershipRequestView(HandleOrganizationMembershipRequestModel model)
        {
            return View("HandleOrganizationMembershipRequest", model);
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> CancelMemberRequest(string accountName, string memberName)
        {
            var account = GetAccount(accountName);

            if (account == null
                || ActionsRequiringPermissions.ManageMembership.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return Json(HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            try
            {
                var removedUser = await UserService.CancelMembershipRequestAsync(account, memberName);
                var emailMessage = new OrganizationMembershipRequestCanceledMessage(MessageServiceConfiguration, account, removedUser);
                await MessageService.SendMessageAsync(emailMessage);
                return Json(Strings.CancelMemberRequest_Success);
            }
            catch (EntityException e)
            {
                return Json(HttpStatusCode.BadRequest, e.Message);
            }
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> UpdateMember(string accountName, string memberName, bool isAdmin)
        {
            var account = GetAccount(accountName);

            if (account == null
                || ActionsRequiringPermissions.ManageMembership.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return Json(HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            if (!account.Confirmed)
            {
                return Json(HttpStatusCode.BadRequest, Strings.Member_OrganizationUnconfirmed);
            }

            try
            {
                var membership = await UserService.UpdateMemberAsync(account, memberName, isAdmin);
                var emailMessage = new OrganizationMemberUpdatedMessage(MessageServiceConfiguration, account, membership);
                await MessageService.SendMessageAsync(emailMessage);

                return Json(new OrganizationMemberViewModel(membership, GetGravatarUrl(membership.Member)));
            }
            catch (EntityException e)
            {
                return Json(HttpStatusCode.BadRequest, e.Message);
            }
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteMember(string accountName, string memberName)
        {
            var account = GetAccount(accountName);

            var currentUser = GetCurrentUser();

            if (account == null ||
                (currentUser.Username != memberName &&
                ActionsRequiringPermissions.ManageMembership.CheckPermissions(currentUser, account)
                    != PermissionsCheckResult.Allowed))
            {
                return Json(HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            if (!account.Confirmed)
            {
                return Json(HttpStatusCode.BadRequest, Strings.Member_OrganizationUnconfirmed);
            }

            try
            {
                var removedMember = await UserService.DeleteMemberAsync(account, memberName);
                var emailMessage = new OrganizationMemberRemovedMessage(MessageServiceConfiguration, account, removedMember);

                await MessageService.SendMessageAsync(emailMessage);

                return Json(Strings.DeleteMember_Success);
            }
            catch (EntityException e)
            {
                return Json(HttpStatusCode.BadRequest, e.Message);
            }
        }

        protected override string GetDeleteAccountViewName() => "DeleteOrganizationAccount";

        protected override DeleteAccountViewModel GetDeleteAccountViewModel(Organization account)
        {
            return GetDeleteOrganizationViewModel(account);
        }

        private DeleteOrganizationViewModel GetDeleteOrganizationViewModel(Organization account)
        {
            var currentUser = base.GetCurrentUser();

            var members = account.Members
                .Select(m => new OrganizationMemberViewModel(m, GetGravatarUrl(m.Member)))
                .ToList();

            var additionalMembers = account.Members
                .Where(m => !m.Member.MatchesUser(currentUser))
                .Select(m => new OrganizationMemberViewModel(m, GetGravatarUrl(m.Member)))
                .ToList();

            return new DeleteOrganizationViewModel(
                account,
                GetOwnedPackagesViewModels(account),
                members,
                additionalMembers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [UIAuthorize]
        public override async Task<ActionResult> RequestAccountDeletion(string accountName = null)
        {
            var account = GetAccount(accountName);
            var currentUser = GetCurrentUser();

            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return new HttpNotFoundResult();
            }

            var model = GetDeleteOrganizationViewModel(account);

            if (model.HasPackagesThatWillBeOrphaned)
            {
                TempData["ErrorMessage"] = "You cannot delete your organization unless you transfer ownership of all of its packages to another account.";

                return RedirectToAction(nameof(DeleteRequest));
            }

            if (model.HasAdditionalMembers)
            {
                TempData["ErrorMessage"] = "You cannot delete your organization unless you remove all other members.";

                return RedirectToAction(nameof(DeleteRequest));
            }

            var result = await DeleteAccountService.DeleteAccountAsync(account, currentUser);

            if (result.Success)
            {
                TempData["Message"] = $"Your organization, '{accountName}', was successfully deleted!";

                return RedirectToAction("Organizations", "Users");
            }
            else
            {
                TempData["ErrorMessage"] = $"There was an issue deleting your organization '{accountName}'. Please contact support for assistance.";

                return RedirectToAction(nameof(DeleteRequest));
            }
        }

        protected override void UpdateAccountViewModel(Organization account, OrganizationAccountViewModel model)
        {
            base.UpdateAccountViewModel(account, model);

            model.Members =
                account.Members.Select(m => new OrganizationMemberViewModel(m, GetGravatarUrl(m.Member)))
                .Concat(account.MemberRequests.Select(m => new OrganizationMemberViewModel(m, GetGravatarUrl(m.NewMember))));

            model.RequiresTenant = account.IsRestrictedToOrganizationTenantPolicy();

            model.CanManageMemberships =
                ActionsRequiringPermissions.ManageMembership.CheckPermissions(GetCurrentUser(), account)
                    == PermissionsCheckResult.Allowed;
        }

        protected override RouteUrlTemplate<string> GetDeleteCertificateForAccountTemplate(string accountName)
        {
            return Url.DeleteOrganizationCertificateTemplate(accountName);
        }

        private string GetGravatarUrl(User user)
        {
            return _features.IsGravatarProxyEnabled()
                ? Url.Avatar(user.Username, GalleryConstants.GravatarElementSize)
                : GravatarHelper.Url(user.EmailAddress, GalleryConstants.GravatarElementSize);
        }
    }
}