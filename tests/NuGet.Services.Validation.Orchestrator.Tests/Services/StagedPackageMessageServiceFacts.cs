// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery;
using NuGetGallery.Infrastructure.Mail.Messages;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    /// <summary>
    /// Verifies private validation message wiring for parent and symbol artifacts.
    /// </summary>
    public class StagedPackageMessageServiceFacts
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SendsPrivateValidationMessages(bool symbols)
        {
            var messages = new List<IEmailBuilder>();
            var sender = new Mock<IMessageService>();
            sender.Setup(service => service.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Callback<IEmailBuilder, bool, bool>((message, copySender, discloseSender) => messages.Add(message))
                .Returns(Task.CompletedTask);
            var options = new Mock<IOptionsSnapshot<EmailConfiguration>>();
            options.SetupGet(value => value.Value).Returns(new EmailConfiguration
            {
                GalleryOwner = "Gallery <support@gallery.test>",
                GalleryNoReplyAddress = "Gallery <noreply@gallery.test>",
                StagedPackageUrlTemplate = "https://staging.test/gallery/account/staging/package/{0}/{1}",
                StagedSymbolPackageUrlTemplate = "https://staging.test/gallery/account/staging/symbols/{0}/{1}",
                PackageUrlTemplate = "https://gallery.test/packages/{0}/{1}",
                PackageSupportTemplate = "https://gallery.test/packages/{0}/{1}/contactowners",
                EmailSettingsUrl = "https://gallery.test/account",
                AnnouncementsUrl = "https://github.com/NuGet/Announcements",
                TwitterUrl = "https://twitter.com/nuget",
            });
            var owner = new Organization("staging-owner") { EmailAddress = "owner@gallery.test", NotifyPackageStaged = true, NotifyPackagePushed = false };
            var identity = new StagedPackageIdentity
            {
                Owner = owner,
                Package = new Package { PackageRegistration = new PackageRegistration { Id = "Test.Package" }, Version = "1.0.0", NormalizedVersion = "1.0.0" },
            };
            var validationSet = new PackageValidationSet { PackageValidations = new List<PackageValidation>() };
            if (symbols)
            {
                var service = new StagedSymbolPackageMessageService(sender.Object, options.Object);
                var attempt = new StagedSymbolPackage { StagedPackageIdentity = identity };
                await service.SendPublishedMessageAsync(attempt);
                await service.SendValidationFailedMessageAsync(attempt, validationSet);
                await service.SendValidationTakingTooLongMessageAsync(attempt);
            }
            else
            {
                var service = new StagedPackageMessageService(sender.Object, options.Object);
                var attempt = new StagedPackage { StagedPackageIdentity = identity };
                await service.SendPublishedMessageAsync(attempt);
                await service.SendValidationFailedMessageAsync(attempt, validationSet);
                await service.SendValidationTakingTooLongMessageAsync(attempt);
            }

            Assert.Equal(3, messages.Count);
            Assert.IsType<StagedPackageValidationSucceededMessage>(messages[0]);
            Assert.IsType<StagedPackageValidationFailedMessage>(messages[1]);
            Assert.IsType<StagedPackageValidationTakingTooLongMessage>(messages[2]);
            var artifact = "package";
            var artifactName = "Staged package";
            if (symbols)
            {
                artifact = "symbols";
                artifactName = "Staged symbol package";
            }
            foreach (var message in messages)
            {
                Assert.Equal(owner.ToMailAddress(), Assert.Single(message.GetRecipients().To));
                Assert.Contains(artifactName, message.GetSubject());
                var body = message.GetBody(EmailFormat.Markdown);
                Assert.Contains($"https://staging.test/gallery/account/staging/{artifact}/Test.Package/1.0.0", body);
                Assert.DoesNotContain("https://gallery.test/packages/", body);
            }
        }
    }
}
