﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;

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
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return Json((int)HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            try
            {
                var membership = await UserService.AddMemberAsync(account, memberName, isAdmin);
                return Json(new OrganizationMemberViewModel(membership));
            }
            catch (EntityException e)
            {
                return Json((int)HttpStatusCode.BadRequest, e.Message);
            }
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> UpdateMember(string accountName, string memberName, bool isAdmin)
        {
            var account = GetAccount(accountName);

            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return Json((int)HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            try
            {
                var membership = await UserService.UpdateMemberAsync(account, memberName, isAdmin);
                return Json(new OrganizationMemberViewModel(membership));
            }
            catch (EntityException e)
            {
                return Json((int)HttpStatusCode.BadRequest, e.Message);
            }
        }

        [HttpPost]
        [UIAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteMember(string accountName, string memberName)
        {
            var account = GetAccount(accountName);

            if (account == null
                || ActionsRequiringPermissions.ManageAccount.CheckPermissions(GetCurrentUser(), account)
                    != PermissionsCheckResult.Allowed)
            {
                return Json((int)HttpStatusCode.Forbidden, Strings.Unauthorized);
            }

            try
            {
                await UserService.DeleteMemberAsync(account, memberName);
                return Json(Strings.DeleteMember_Success);
            }
            catch (EntityException e)
            {
                return Json((int)HttpStatusCode.BadRequest, e.Message);
            }
        }

        protected override void UpdateAccountViewModel(Organization account, OrganizationAccountViewModel model)
        {
            base.UpdateAccountViewModel(account, model);

            model.Members = account.Members.Select(m => new OrganizationMemberViewModel(m));
        }
    }
}