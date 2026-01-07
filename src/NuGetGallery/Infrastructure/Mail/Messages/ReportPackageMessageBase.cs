// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Mail;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Infrastructure.Mail.Requests;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ReportPackageMessageBase : MarkdownEmailBuilder
    {
        public ReportPackageMessageBase(
            IMessageServiceConfiguration configuration,
            ReportPackageRequest request)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        protected IMessageServiceConfiguration Configuration { get; }

        public ReportPackageRequest Request { get; }

        public override MailAddress Sender => Configuration.GalleryOwner;

        public override IEmailRecipients GetRecipients()
        {
            var cc = new List<MailAddress>();
            if (Request.CopySender)
            {
                // Normally we use a second email to copy the sender to avoid disclosing the receiver's address
                // but here, the receiver is the gallery operators who already disclose their address
                // CCing helps to create a thread of email that can be augmented by the sending user
                cc.Add(Request.FromAddress);
            }

            return new EmailRecipients(
                to: new[] { Sender },
                cc: cc,
                replyTo: new[] { Request.FromAddress });
        }

        public override string GetSubject()
        {
            throw new NotImplementedException();
        }

        protected override string GetMarkdownBody()
        {
            throw new NotImplementedException();
        }
    }
}
