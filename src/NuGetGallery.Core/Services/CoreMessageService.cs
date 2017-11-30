// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using AnglicanGeek.MarkdownMailer;

namespace NuGetGallery.Services
{
    public class CoreMessageService : ICoreMessageService
    {
        protected CoreMessageService()
        {
        }

        public CoreMessageService(IMailSender mailSender, ICoreMessageServiceConfiguration coreConfiguration)
        {
            MailSender = mailSender ?? throw new ArgumentNullException(nameof(mailSender));
            CoreConfiguration = coreConfiguration ?? throw new ArgumentNullException(nameof(coreConfiguration));
        }

        public IMailSender MailSender { get; protected set; }
        public ICoreMessageServiceConfiguration CoreConfiguration { get; protected set; }

        public void SendPackageAddedNotice(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl)
        {
            string subject = "[{0}] Package published - {1} {2}";
            string body = @"The package [{1} {2}]({3}) was just published on {0}. If this was not intended, please [contact support]({4}).

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving emails as an owner of this package, sign in to the {0} and
    [change your email notification settings]({5}).
</em>";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                CoreConfiguration.GalleryOwner.DisplayName,
                package.PackageRegistration.Id,
                package.Version,
                packageUrl,
                packageSupportUrl,
                emailSettingsUrl);

            subject = String.Format(CultureInfo.CurrentCulture, subject, CoreConfiguration.GalleryOwner.DisplayName, package.PackageRegistration.Id, package.Version);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = CoreConfiguration.GalleryNoReplyAddress;

                AddOwnersSubscribedToPackagePushedNotification(package.PackageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    SendMessage(mailMessage, copySender: false);
                }
            }
        }

        protected static void AddOwnersToMailMessage(PackageRegistration packageRegistration, MailMessage mailMessage)
        {
            foreach (var owner in packageRegistration.Owners.Where(o => o.EmailAllowed))
            {
                mailMessage.To.Add(owner.ToMailAddress());
            }
        }

        protected static void AddOwnersSubscribedToPackagePushedNotification(PackageRegistration packageRegistration, MailMessage mailMessage)
        {
            foreach (var owner in packageRegistration.Owners.Where(o => o.NotifyPackagePushed))
            {
                mailMessage.To.Add(owner.ToMailAddress());
            }
        }

        virtual protected void SendMessage(MailMessage mailMessage, bool copySender)
        {
            MailSender.Send(mailMessage);
            if (copySender)
            {
                var senderCopy = new MailMessage(
                    CoreConfiguration.GalleryOwner,
                    mailMessage.ReplyToList.First())
                {
                    Subject = mailMessage.Subject + " [Sender Copy]",
                    Body = String.Format(
                            CultureInfo.CurrentCulture,
                            "You sent the following message via {0}: {1}{1}{2}",
                            CoreConfiguration.GalleryOwner.DisplayName,
                            Environment.NewLine,
                            mailMessage.Body),
                };
                senderCopy.ReplyToList.Add(mailMessage.ReplyToList.First());
                MailSender.Send(senderCopy);
            }
        }
    }
}
