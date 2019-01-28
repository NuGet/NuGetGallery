// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using NuGet.Services.Entities;

namespace NuGetGallery.Infrastructure.Mail
{
    public static class EmailRecipientsHelper
    {
        public static IReadOnlyList<MailAddress> GetAllOwners(PackageRegistration packageRegistration, bool requireEmailAllowed)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            var recipients = new List<MailAddress>();
            var owners = requireEmailAllowed
                ? packageRegistration.Owners.Where(o => o.EmailAllowed)
                : packageRegistration.Owners;

            foreach (var owner in owners)
            {
                recipients.Add(owner.ToMailAddress());
            }

            return recipients;
        }

        public static IReadOnlyList<MailAddress> GetOwnersSubscribedToPackagePushedNotification(PackageRegistration packageRegistration)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            var recipients = new List<MailAddress>();
            foreach (var owner in packageRegistration.Owners.Where(o => o.NotifyPackagePushed))
            {
                recipients.Add(owner.ToMailAddress());
            }

            return recipients;
        }
    }
}