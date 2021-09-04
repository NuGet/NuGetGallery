// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using NuGet.Services.Entities;

namespace NuGetGallery.Infrastructure.Mail
{
    public static class GalleryEmailRecipientsUtility
    {
        public static IReadOnlyList<MailAddress> GetAddressesWithPermission(User user, ActionRequiringAccountPermissions action)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var recipients = new List<MailAddress>();

            if (user is Organization organization)
            {
                var membersAllowedToAct = organization.Members
                    .Where(m => action.CheckPermissions(m.Member, m.Organization) == PermissionsCheckResult.Allowed)
                    .Select(m => m.Member);

                foreach (var member in membersAllowedToAct)
                {
                    if (!member.EmailAllowed)
                    {
                        continue;
                    }

                    recipients.Add(member.ToMailAddress());
                }
            }
            else if (user.EmailAllowed)
            {
                recipients.Add(user.ToMailAddress());
            }

            return recipients;
        }
    }
}