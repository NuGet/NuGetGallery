// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    /// <summary>
    /// Verifies expiration outcomes, private owner actions, safe rendering and staging preferences.
    /// </summary>
    public class StagingExpirationMessageFacts : MarkdownMessageBuilderFacts
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DescribesExpirationWithOwnerOnlyRecipientsAndFormatSpecificActions(bool deleted)
        {
            var owner = new Organization("owner_name") { EmailAddress = "owner@example.test", NotifyPackageStaged = true, NotifyPackagePushed = false, EmailAllowed = false };
            var deadline = new DateTime(2026, 10, 6, 12, 30, 0, DateTimeKind.Utc);
            var description = "staging group Group_A.Test <script>alert(1)</script>";
            var message = new StagingExpirationMessage(Configuration, owner, description, deadline, deleted,
                "https://gallery.test/account/Packages", "https://gallery.test/account");

            Assert.Equal(owner.ToMailAddress(), Assert.Single(message.GetRecipients().To));
            Assert.Contains(deleted ? "expired" : "expires soon", message.GetSubject());
            foreach (var format in new[] { EmailFormat.Markdown, EmailFormat.Html, EmailFormat.PlainText })
            {
                var body = message.GetBody(format);
                Assert.Contains("staging group", body);
                Assert.Contains("owner_name", body);
                Assert.Contains("https://gallery.test/account/Packages", body);
                Assert.DoesNotContain("/packages/", body);
                Assert.EndsWith(EmailMessageFooter.ForPackageOwnerNotifications(format, Configuration.GalleryOwner.DisplayName,
                    "https://gallery.test/account"), body);
                if (deleted)
                {
                    Assert.Contains("has expired", body);
                    Assert.Contains("Upload again", body);
                    Assert.Contains("Published packages and symbols have not been changed", body);
                }
                else
                {
                    Assert.Contains("2026-10-06 12:30:00 UTC", body);
                    Assert.Contains("promote it before the deadline", body);
                }

                if (format == EmailFormat.Html)
                {
                    Assert.DoesNotContain("<script>", body);
                }
                else if (format == EmailFormat.PlainText)
                {
                    Assert.Contains(description, body);
                }
            }

            owner.NotifyPackageStaged = false;
            Assert.Empty(message.GetRecipients().To);
            owner.NotifyPackageStaged = true;
            owner.IsDeleted = true;
            Assert.Empty(message.GetRecipients().To);
        }
    }
}
