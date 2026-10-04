// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGet.Services.Validation;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    /// <summary>
    /// Verifies staging notification privacy, subscription rules, and actionable copy.
    /// </summary>
    public class StagedPackageMessageFacts : MarkdownMessageBuilderFacts
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UsesOnlyTheStagingOwnerAndIndependentStagingSubscription(bool subscribed)
        {
            var owner = CreateOwner(subscribed);
            var package = CreatePackage();
            var uploaded = new StagedPackageUploadedMessage(Configuration, owner, package, true, GetStagingUrl(true), "https://gallery.test/account");
            var delayed = new StagedPackageValidationTakingTooLongMessage(Configuration, owner, package, true, GetStagingUrl(true), "https://gallery.test/account");
            var succeeded = new StagedPackageValidationSucceededMessage(Configuration, owner, package, true, GetStagingUrl(true), "https://gallery.test/account");
            var failed = CreateFailed(owner, package, true, ValidationIssueCode.Unknown);

            Assert.Equal(subscribed ? 1 : 0, uploaded.GetRecipients().To.Count);
            Assert.Equal(subscribed ? 1 : 0, delayed.GetRecipients().To.Count);
            Assert.Equal(subscribed ? 1 : 0, succeeded.GetRecipients().To.Count);
            Assert.Equal(owner.ToMailAddress(), Assert.Single(failed.GetRecipients().To));
            if (subscribed)
            {
                Assert.Equal(owner.ToMailAddress(), Assert.Single(uploaded.GetRecipients().To));
                Assert.Equal(owner.ToMailAddress(), Assert.Single(delayed.GetRecipients().To));
                Assert.Equal(owner.ToMailAddress(), Assert.Single(succeeded.GetRecipients().To));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UsesPrivateDetailsLinksAndDoesNotPromisePublication(bool symbols)
        {
            var owner = CreateOwner(true);
            var package = CreatePackage();
            var messages = new MarkdownEmailBuilder[]
            {
                new StagedPackageUploadedMessage(Configuration, owner, package, symbols, GetStagingUrl(symbols), "https://gallery.test/account"),
                new StagedPackageValidationTakingTooLongMessage(Configuration, owner, package, symbols, GetStagingUrl(symbols), "https://gallery.test/account"),
                CreateFailed(owner, package, symbols, ValidationIssueCode.PackageIsZip64),
                new StagedPackageValidationSucceededMessage(Configuration, owner, package, symbols, GetStagingUrl(symbols), "https://gallery.test/account"),
            };
            var artifact = "package";
            if (symbols)
            {
                artifact = "symbols";
            }

            foreach (var message in messages)
            {
                foreach (var format in new[] { EmailFormat.Markdown, EmailFormat.Html, EmailFormat.PlainText })
                {
                    var body = message.GetBody(format);
                    Assert.Contains($"https://gallery.test/account/staging/{artifact}/Test.Package/1.0.0", body);
                    Assert.Contains("private", body);
                    Assert.DoesNotContain("https://gallery.test/packages/", body);
                    Assert.DoesNotContain("was recently published", body);
                    Assert.DoesNotContain("when your package has been published", body);
                    Assert.DoesNotContain("ready to promote", body);
                    if (!(message is StagedPackageValidationFailedMessage))
                    {
                        var footer = EmailMessageFooter.ForPackageOwnerNotifications(format, Configuration.GalleryOwner.DisplayName, "https://gallery.test/account");
                        Assert.EndsWith(footer, body);
                        if (format == EmailFormat.PlainText)
                        {
                            Assert.DoesNotContain("[change your email notification settings]", body);
                        }
                    }
                }
            }

            Assert.Contains("staged", messages[0].GetSubject());
            Assert.Contains("validation taking longer", messages[1].GetSubject());
            Assert.Contains("validation failed", messages[2].GetSubject());
            Assert.Contains("validation passed", messages[3].GetSubject());
        }

        [Theory]
        [InlineData(ValidationIssueCode.Unknown, "Contact support")]
        [InlineData(ValidationIssueCode.PackageIsZip64, "Fix the reported issues, then replace the staged content.")]
        public void GivesAppropriateFailureNextSteps(ValidationIssueCode code, string expectedAction)
        {
            var message = CreateFailed(CreateOwner(false), CreatePackage(), false, code);

            var body = message.GetBody(EmailFormat.Markdown);

            Assert.Contains(expectedAction, body);
            Assert.Contains("cannot be promoted until validation succeeds", body);
            if (code == ValidationIssueCode.PackageIsZip64)
            {
                Assert.Contains("Zip64 packages are not supported.", body);
            }
        }

        private static User CreateOwner(bool subscribed)
        {
            return new Organization("staging-owner") { EmailAddress = "staging-owner@gallery.test", NotifyPackageStaged = subscribed, NotifyPackagePushed = !subscribed, EmailAllowed = false };
        }

        private static Package CreatePackage()
        {
            return new Package
            {
                PackageRegistration = new PackageRegistration
                {
                    Id = "Test.Package",
                    Owners = new List<User> { new User("other-owner") { EmailAddress = "other@gallery.test", NotifyPackagePushed = true, NotifyPackageStaged = true } },
                },
                Version = "1.0.0",
                NormalizedVersion = "1.0.0",
            };
        }

        private static StagedPackageValidationFailedMessage CreateFailed(User owner, Package package, bool symbols, ValidationIssueCode code)
        {
            var validationSet = new PackageValidationSet
            {
                PackageValidations = new List<PackageValidation>
                {
                    new PackageValidation { PackageValidationIssues = new List<PackageValidationIssue> { new PackageValidationIssue { IssueCode = code } } },
                },
            };
            return new StagedPackageValidationFailedMessage(Configuration, owner, package, symbols, GetStagingUrl(symbols), validationSet,
                "https://github.com/NuGet/Announcements", "https://twitter.com/nuget");
        }

        private static string GetStagingUrl(bool symbols)
        {
            var artifact = "package";
            if (symbols)
            {
                artifact = "symbols";
            }

            return $"https://gallery.test/account/staging/{artifact}/Test.Package/1.0.0";
        }
    }
}
