// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    /// <summary>
    /// Provides private artifact links and staging-owner recipients for staging notifications.
    /// </summary>
    public abstract class StagedPackageMessage : MarkdownEmailBuilder
    {
        protected StagedPackageMessage(IMessageServiceConfiguration configuration, User owner, Package package, bool symbols, string stagingUrl)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Package = package ?? throw new ArgumentNullException(nameof(package));
            if (string.IsNullOrWhiteSpace(stagingUrl))
            {
                throw new ArgumentException("The staging URL is required.", nameof(stagingUrl));
            }

            var url = new Uri(stagingUrl, UriKind.Absolute);
            if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
            {
                throw new ArgumentException("The staging URL must be an HTTP or HTTPS URL.", nameof(stagingUrl));
            }

            ArtifactName = "Package";
            if (symbols)
            {
                ArtifactName = "Symbol package";
            }

            StagingUrl = url.AbsoluteUri;
        }

        protected IMessageServiceConfiguration Configuration { get; }

        protected User Owner { get; }

        protected Package Package { get; }

        protected string ArtifactName { get; }

        protected string StagingUrl { get; }

        public override MailAddress Sender => Configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            if (!Owner.NotifyPackageStaged)
            {
                return new EmailRecipients(Array.Empty<MailAddress>());
            }

            return new EmailRecipients(new[] { Owner.ToMailAddress() });
        }
    }
}
