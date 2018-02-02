// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public class OrganizationsController
        : AccountsController<Organization, OrganizationAccountViewModel>
    {
        public OrganizationsController(
            AuthenticationService authService,
            ICuratedFeedService curatedFeedService,
            IMessageService messageService,
            IUserService userService)
            : base(authService, curatedFeedService, messageService, userService)
        {
        }

        public override string AccountAction => nameof(ManageOrganization);

        protected internal override ViewMessages Messages => new ViewMessages
        {
            EmailConfirmed = Strings.OrganizationEmailConfirmed,
            EmailPreferencesUpdated = Strings.OrganizationEmailPreferencesUpdated,
            EmailUpdateCancelled = Strings.OrganizationEmailUpdateCancelled,
            EmailUpdated = Strings.OrganizationEmailUpdated,
            EmailUpdatedWithConfirmationRequired = Strings.OrganizationEmailUpdatedWithConfirmationRequired
        };

        [HttpGet]
        [Authorize]
        public virtual ActionResult ManageOrganization(string accountName)
        {
            var account = GetAccount(accountName);

            return AccountView(account);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AddOrUpdateMember(string accountName, string memberName, bool isAdmin)
        {
            var account = GetAccount(accountName);

            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return Json(Strings.Unauthorized);
            }

            try
            {
                var membership = await UserService.AddOrUpdateMemberAsync(account, memberName, isAdmin);
                return Json(new OrganizationMemberViewModel(membership));
            }
            catch (EntityException e)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(e.Message);
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteMember(string accountName, string memberName)
        {
            var account = GetAccount(accountName);

            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return Json(Strings.Unauthorized);
            }

            try
            {
                await UserService.DeleteMemberAsync(account, memberName);
                return Json(Strings.DeleteMember_Success);
            }
            catch (EntityException e)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(e.Message);
            }
        }

        protected override void UpdateAccountViewModel(Organization account, OrganizationAccountViewModel model)
        {
            base.UpdateAccountViewModel(account, model);

            model.Members = account.Members.Select(m => new OrganizationMemberViewModel(m));
        }
    }
}