// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
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

        protected override void UpdateAccountViewModel(Organization account, OrganizationAccountViewModel model)
        {
            base.UpdateAccountViewModel(account, model);

            model.Members = account.Members.Select(m => new OrganizationMemberViewModel(m));
        }
    }
}