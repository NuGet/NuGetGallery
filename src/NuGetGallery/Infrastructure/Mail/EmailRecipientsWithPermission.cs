// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail
{
    public class EmailRecipientsWithPermission
        : IEmailRecipients
    {
        public EmailRecipientsWithPermission(
            User user,
            ActionRequiringAccountPermissions action,
            IReadOnlyList<MailAddress> cc = null,
            IReadOnlyList<MailAddress> bcc = null,
            IReadOnlyList<MailAddress> replyTo = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            To = AddAddressesWithPermission(user, action);

            CC = cc ?? new List<MailAddress>();
            Bcc = bcc ?? new List<MailAddress>();
            ReplyTo = replyTo ?? new List<MailAddress>();
        }

        public IReadOnlyList<MailAddress> To { get; }

        public IReadOnlyList<MailAddress> CC { get; }

        public IReadOnlyList<MailAddress> Bcc { get; }

        public IReadOnlyList<MailAddress> ReplyTo { get; }

        private static IReadOnlyList<MailAddress> AddAddressesWithPermission(User user, ActionRequiringAccountPermissions action)
        {
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