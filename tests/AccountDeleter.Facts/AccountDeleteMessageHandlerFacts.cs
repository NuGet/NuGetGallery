// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.AccountDeleter.Facts
{
    public class AccountDeleteMessageHandlerFacts
    {
        [Fact]
        public async Task IgnoresMissingUser()
        {
            User = null;

            var processed = await Target.HandleAsync(Message);

            Assert.True(processed);
            TelemetryService.Verify(x => x.TrackUserNotFound(Message.Source), Times.Once);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        public async Task DeletesUserAndAlwaysSendsEmailWhenIgnoringContactSetting(bool emailAllowed, bool deleteSuccess)
        {
            Config.RespectEmailContactSetting = false;
            User.EmailAllowed = emailAllowed;
            AccountManager.Setup(x => x.DeleteAccount(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(deleteSuccess);

            var processed = await Target.HandleAsync(Message);

            Assert.True(processed);
            TelemetryService.Verify(x => x.TrackDeleteResult(Message.Source, deleteSuccess), Times.Once);
            MessageService.Verify(x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), false, false), Times.Once);
            TelemetryService.Verify(x => x.TrackEmailSent(Message.Source, emailAllowed), Times.Once);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        public async Task DeletesUserAndAndCanRespsectEmailAllowedSetting(bool emailAllowed, bool deleteSuccess)
        {
            Config.RespectEmailContactSetting = true;
            User.EmailAllowed = emailAllowed;
            AccountManager.Setup(x => x.DeleteAccount(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(deleteSuccess);

            var processed = await Target.HandleAsync(Message);

            Assert.True(processed);
            TelemetryService.Verify(x => x.TrackDeleteResult(Message.Source, deleteSuccess), Times.Once);
            MessageService.Verify(
                x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()),
                Times.Exactly(emailAllowed ? 1 : 0));
            TelemetryService.Verify(x => x.TrackEmailSent(It.IsAny<string>(), It.IsAny<bool>()), Times.Exactly(emailAllowed ? 1 : 0));
            TelemetryService.Verify(x => x.TrackEmailBlocked(It.IsAny<string>()), Times.Exactly(emailAllowed ? 0 : 1));
        }

        [Fact]
        public async Task DoesNotSendEmailWhenUserHasNoConfirmedEmailAddress()
        {
            User.EmailAddress = null;

            var processed = await Target.HandleAsync(Message);

            Assert.True(processed);
            TelemetryService.Verify(x => x.TrackDeleteResult(Message.Source, true), Times.Once);
            TelemetryService.Verify(x => x.TrackUnconfirmedUser(Message.Source), Times.Once);
            MessageService.Verify(x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            TelemetryService.Verify(x => x.TrackEmailSent(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        public AccountDeleteMessageHandlerFacts(ITestOutputHelper output)
        {
            Options = new Mock<IOptionsSnapshot<AccountDeleteConfiguration>>();
            AccountManager = new Mock<IAccountManager>();
            UserService = new Mock<IUserService>();
            MessageService = new Mock<IMessageService>();
            EmailBuilderFactory = new Mock<IEmailBuilderFactory>();
            TelemetryService = new Mock<IAccountDeleteTelemetryService>();

            Message = new AccountDeleteMessage("frank", "TheMoon");
            Config = new AccountDeleteConfiguration
            {
                RespectEmailContactSetting = false,
                EmailConfiguration = new EmailConfiguration
                {
                    GalleryOwner = "gallery@example",
                },
                SourceConfigurations = new List<SourceConfiguration>
                {
                    new SourceConfiguration
                    {
                        SourceName = Message.Source,
                    },
                },
            };
            User = new User
            {
                Username = Message.Username,
                EmailAddress = "frank@example",
                EmailAllowed = true,
            };
            BaseEmailBuilder = new Mock<IEmailBuilder>();

            Options.Setup(x => x.Value).Returns(() => Config);
            UserService.Setup(x => x.FindByUsername(It.IsAny<string>(), It.IsAny<bool>())).Returns(() => User);
            AccountManager.Setup(x => x.DeleteAccount(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(true);
            EmailBuilderFactory.Setup(x => x.GetEmailBuilder(It.IsAny<string>(), It.IsAny<bool>())).Returns(() => BaseEmailBuilder.Object);

            var loggerFactory = new LoggerFactory().AddXunit(output);

            Target = new AccountDeleteMessageHandler(
                Options.Object,
                AccountManager.Object,
                UserService.Object,
                MessageService.Object,
                EmailBuilderFactory.Object,
                TelemetryService.Object,
                loggerFactory.CreateLogger<AccountDeleteMessageHandler>());
        }

        public Mock<IOptionsSnapshot<AccountDeleteConfiguration>> Options { get; }
        public Mock<IAccountManager> AccountManager { get; }
        public Mock<IUserService> UserService { get; }
        public Mock<IMessageService> MessageService { get; }
        public Mock<IEmailBuilderFactory> EmailBuilderFactory { get; }
        public Mock<IAccountDeleteTelemetryService> TelemetryService { get; }
        public AccountDeleteConfiguration Config { get; }
        public AccountDeleteMessageHandler Target { get; }
        public AccountDeleteMessage Message { get; }
        public User User { get; set; }
        public Mock<IEmailBuilder> BaseEmailBuilder { get; }
    }
}
