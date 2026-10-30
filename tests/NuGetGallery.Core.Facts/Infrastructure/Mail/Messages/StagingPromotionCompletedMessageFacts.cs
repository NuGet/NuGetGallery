// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    /// <summary>
    /// Verifies per-artifact results, staging-owner recipients, and format-specific actions.
    /// </summary>
    public class StagingPromotionCompletedMessageFacts : MarkdownMessageBuilderFacts
    {
        [Theory]
        [InlineData(false, true, true, true)]
        [InlineData(false, true, false, false)]
        [InlineData(false, false, false, true)]
        [InlineData(true, false, false, true)]
        public void ReportsOnlyOneArtifactAndUsesStagingRecipientPolicy(bool symbols, bool succeeded, bool subscribed, bool sends)
        {
            var owner = new Organization("owner_name") { EmailAddress = "owner@example.test", NotifyPackageStaged = subscribed, NotifyPackagePushed = !subscribed, EmailAllowed = false };
            var package = new Package { PackageRegistration = new PackageRegistration { Id = "Package_A.Test" }, NormalizedVersion = "1.0.0" };
            var artifact = new StagingPromotionArtifact(package, symbols, succeeded);
            package.PackageRegistration = null;
            var message = new StagingPromotionCompletedMessage(Configuration, owner, artifact, "https://gallery.test/packages/{0}/{1}",
                "https://gallery.test/account/Packages", "https://gallery.test/account");
            var kind = "package";
            if (symbols)
            {
                kind = "symbol package";
            }

            Assert.Contains($"Staged {kind} promotion", message.GetSubject());
            Assert.Contains(succeeded ? "succeeded" : "failed", message.GetSubject());
            Assert.Equal(sends ? 1 : 0, message.GetRecipients().To.Count);
            if (sends)
            {
                Assert.Equal(owner.ToMailAddress(), Assert.Single(message.GetRecipients().To));
            }

            foreach (var format in new[] { EmailFormat.Markdown, EmailFormat.Html, EmailFormat.PlainText })
            {
                var body = message.GetBody(format);
                Assert.Contains($"staged {kind}", body);
                Assert.Contains("Package_A.Test", body);
                Assert.Contains("owner_name", body);
                Assert.Contains("1.0.0", body);
                Assert.Contains("https://gallery.test/account/Packages", body);
                Assert.DoesNotContain("group", body);
                Assert.DoesNotContain("history", body);
                Assert.DoesNotContain("queued", body);
                if (succeeded)
                {
                    Assert.Contains("has been published", body);
                    Assert.Contains("https://gallery.test/packages/Package_A.Test/1.0.0", body);
                    Assert.EndsWith(EmailMessageFooter.ForPackageOwnerNotifications(format, Configuration.GalleryOwner.DisplayName,
                        "https://gallery.test/account"), body);
                }
                else
                {
                    Assert.Contains("could not be published", body);
                    Assert.Contains("before retrying promotion", body);
                    Assert.DoesNotContain("https://gallery.test/packages/", body);
                    Assert.DoesNotContain("change your email notification settings", body);
                }
            }
        }

        [Fact]
        public void DoesNotNotifyDeletedOwnerEvenForMandatoryFailure()
        {
            var owner = new User("deleted") { IsDeleted = true, NotifyPackageStaged = true };
            var package = new Package { PackageRegistration = new PackageRegistration { Id = "PackageA" }, NormalizedVersion = "1.0.0" };
            var message = new StagingPromotionCompletedMessage(Configuration, owner, new StagingPromotionArtifact(package, false, false),
                "https://gallery.test/packages/{0}/{1}", "https://gallery.test/account/Packages", "https://gallery.test/account");

            Assert.Empty(message.GetRecipients().To);
        }
    }
}
