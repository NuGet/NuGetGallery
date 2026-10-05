// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGet.Services.Staging;
using NuGet.Services.Validation;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Staging.Promotion.Tests
{
    public class StagedSymbolPackagePromotionMessageHandlerFacts
    {
        [Fact]
        public async Task JobDispatchesAcceptedSymbolsToSeparateOrchestratorTopic()
        {
            var fixture = new Fixture();
            IBrokeredMessage symbolsMessage = null;
            var symbolsTopic = new Mock<ITopicClient>();
            symbolsTopic.Setup(topic => topic.SendAsync(It.IsAny<IBrokeredMessage>()))
                .Callback<IBrokeredMessage>(message => symbolsMessage = message).Returns(Task.CompletedTask);
            var promotionTopic = new Mock<ITopicClient>();
            promotionTopic.Setup(topic => topic.SendAsync(It.IsAny<IBrokeredMessage>())).Returns(Task.CompletedTask);
            var builder = new ContainerBuilder();
            new JobHarness().Configure(builder);
            builder.RegisterInstance(symbolsTopic.Object).Keyed<ITopicClient>("SymbolsOrchestratorTopic");
            builder.RegisterInstance(promotionTopic.Object).Keyed<ITopicClient>("PromotionTopic");
            builder.RegisterInstance(fixture.Attempts.Object).As<IEntityRepository<StagedSymbolPackage>>();
            builder.RegisterInstance(Mock.Of<IEntityRepository<StagedPackage>>()).As<IEntityRepository<StagedPackage>>();
            builder.RegisterInstance(Mock.Of<IEntityRepository<StagedPackageIdentity>>()).As<IEntityRepository<StagedPackageIdentity>>();
            builder.RegisterInstance(Mock.Of<IEntityRepository<StagingGroup>>()).As<IEntityRepository<StagingGroup>>();
            builder.RegisterInstance(Mock.Of<IStagingBlobCleanupService>()).As<IStagingBlobCleanupService>();
            builder.RegisterInstance(Mock.Of<IStagingPromotionNotificationService>()).As<IStagingPromotionNotificationService>();
            builder.RegisterInstance(Mock.Of<IStagingPromotionMessageHandler<StagingGroup>>()).As<IStagingPromotionMessageHandler<StagingGroup>>();
            builder.RegisterInstance(Mock.Of<IStagingPromotionMessageHandler<StagedPackage>>()).As<IStagingPromotionMessageHandler<StagedPackage>>();
            using (var container = builder.Build())
            {
                Assert.True(await container.Resolve<IMessageHandler<StagingPromotionMessage>>().HandleAsync(fixture.Message));

                Assert.NotNull(symbolsMessage);
                promotionTopic.Verify(topic => topic.SendAsync(It.IsAny<IBrokeredMessage>()), Times.Never);
                var received = new Mock<IReceivedBrokeredMessage>();
                received.Setup(message => message.GetBody()).Returns(symbolsMessage.GetBody());
                received.SetupGet(message => message.Properties).Returns(new Dictionary<string, object>(symbolsMessage.Properties));
                var validation = container.Resolve<IServiceBusMessageSerializer>().DeserializePackageValidationMessageData(received.Object);
                Assert.Equal(ValidatingType.StagedSymbolPackage, validation.ProcessValidationSet.ValidatingType);
                Assert.Equal(fixture.Attempt.Key, validation.ProcessValidationSet.EntityKey);
                Assert.Equal(SymbolPromotionValidationTrackingId.Create(fixture.Message.PromotionId, fixture.Attempt.Key), validation.ProcessValidationSet.ValidationTrackingId);

                await container.Resolve<IStagingPromotionMessageEnqueuer>().SendMessageAsync(StagingPromotionMessage.ForPackage(Guid.NewGuid(), 12));

                promotionTopic.Verify(topic => topic.SendAsync(It.IsAny<IBrokeredMessage>()), Times.Once);
                symbolsTopic.Verify(topic => topic.SendAsync(It.IsAny<IBrokeredMessage>()), Times.Once);
            }
        }

        [Fact]
        public async Task JobDeliversPromotionResultThroughConfiguredEmailTopic()
        {
            IBrokeredMessage emailMessage = null;
            var emailTopic = new Mock<ITopicClient>();
            emailTopic.Setup(topic => topic.SendAsync(It.IsAny<IBrokeredMessage>()))
                .Callback<IBrokeredMessage>(message => emailMessage = message).Returns(Task.CompletedTask);
            var builder = new ContainerBuilder();
            new JobHarness().Configure(builder, new Dictionary<string, string>
            {
                ["Email:PackageUrlTemplate"] = "https://gallery.test/packages/{0}/{1}",
                ["Email:ManagePackagesUrl"] = "https://gallery.test/account/Packages",
                ["Email:EmailSettingsUrl"] = "https://gallery.test/account",
            });
            builder.RegisterInstance(emailTopic.Object).Keyed<ITopicClient>("EmailTopic");
            var owner = new User("owner") { EmailAddress = "owner@example.test", NotifyPackageStaged = true };
            var package = new Package { PackageRegistration = new PackageRegistration { Id = "PackageA" }, NormalizedVersion = "1.0.0" };
            using (var container = builder.Build())
            {
                await container.Resolve<IStagingPromotionNotificationService>()
                    .SendAsync(owner, new StagingPromotionArtifact(package, false, true));

                Assert.NotNull(emailMessage);
                var received = new Mock<IReceivedBrokeredMessage>();
                received.Setup(message => message.GetBody()).Returns(emailMessage.GetBody());
                received.SetupGet(message => message.Properties).Returns(new Dictionary<string, object>(emailMessage.Properties));
                var email = container.Resolve<NuGet.Services.Messaging.IServiceBusMessageSerializer>().DeserializeEmailMessageData(received.Object);
                Assert.Equal(owner.EmailAddress, Assert.Single(email.To));
                Assert.Contains("promotion succeeded", email.Subject);
                Assert.Contains("https://gallery.test/packages/PackageA/1.0.0", email.PlainTextBody);
                Assert.Contains("https://gallery.test/account/Packages", email.PlainTextBody);
                emailTopic.Verify(topic => topic.SendAsync(It.IsAny<IBrokeredMessage>()), Times.Once);
            }
        }

        [Fact]
        public void TrackingIdentityIsStableAcrossCulturesAndDistinctForEachAcceptedAttempt()
        {
            var promotionId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                var trackingId = SymbolPromotionValidationTrackingId.Create(promotionId, 43);
                Assert.Equal(Guid.Parse("9b90e985-72a0-b742-0a92-d8622bdd5047"), trackingId);
                Assert.NotEqual(trackingId, SymbolPromotionValidationTrackingId.Create(promotionId, 44));
                Assert.NotEqual(trackingId, SymbolPromotionValidationTrackingId.Create(Guid.NewGuid(), 43));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Fact]
        public async Task RepeatedDispatchUsesSameValidationIdentityAndStagedAttemptKey()
        {
            var fixture = new Fixture();
            var messages = new List<PackageValidationMessageData>();
            fixture.Orchestrator.Setup(service => service.SendMessageAsync(It.IsAny<PackageValidationMessageData>()))
                .Callback<PackageValidationMessageData>(messages.Add).Returns(Task.CompletedTask);

            Assert.True(await fixture.Target.HandleAsync(fixture.Message));
            Assert.True(await fixture.Target.HandleAsync(fixture.Message));

            Assert.Equal(2, messages.Count);
            Assert.Equal(messages[0].ProcessValidationSet.ValidationTrackingId, messages[1].ProcessValidationSet.ValidationTrackingId);
            Assert.All(messages, message =>
            {
                Assert.Equal(ValidatingType.StagedSymbolPackage, message.ProcessValidationSet.ValidatingType);
                Assert.Equal(fixture.Attempt.Key, message.ProcessValidationSet.EntityKey);
                Assert.Equal("PackageA", message.ProcessValidationSet.PackageId);
                Assert.Equal("1.0.0", message.ProcessValidationSet.PackageNormalizedVersion);
            });
            Assert.Equal(StagedPackageStatus.Promoting, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Staged, fixture.Attempt.SymbolPackage.StatusKey);
            fixture.Attempts.Verify(repository => repository.CommitChangesAsync(), Times.Never);
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("superseded")]
        [InlineData("new-promotion")]
        [InlineData("ready")]
        public async Task InactiveCommandsDoNotDispatch(string scenario)
        {
            var fixture = new Fixture();
            switch (scenario)
            {
                case "missing":
                    fixture.Attempts.Setup(repository => repository.GetAll()).Returns(Array.Empty<StagedSymbolPackage>().AsQueryable());
                    break;
                case "superseded":
                    fixture.Attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey++;
                    break;
                case "new-promotion":
                    fixture.Attempt.ActivePromotionId = Guid.NewGuid();
                    break;
                case "ready":
                    fixture.Attempt.Status = StagedPackageStatus.Ready;
                    break;
            }

            Assert.True(await fixture.Target.HandleAsync(fixture.Message));

            fixture.Orchestrator.Verify(service => service.SendMessageAsync(It.IsAny<PackageValidationMessageData>()), Times.Never);
            fixture.Attempts.Verify(repository => repository.CommitChangesAsync(), Times.Never);
        }

        [Theory]
        [InlineData("parent")]
        [InlineData("owner")]
        [InlineData("symbol")]
        public async Task LostEligibilityFailsPromotionWithoutDispatch(string scenario)
        {
            var fixture = new Fixture();
            var parent = fixture.Attempt.StagedPackageIdentity.Package;
            switch (scenario)
            {
                case "parent":
                    parent.PackageStatusKey = PackageStatus.Deleted;
                    break;
                case "owner":
                    parent.PackageRegistration.Owners.Clear();
                    break;
                case "symbol":
                    fixture.Attempt.SymbolPackage.StatusKey = PackageStatus.Deleted;
                    break;
            }

            var parentStatus = parent.PackageStatusKey;
            await fixture.Target.HandleAsync(fixture.Message);
            await fixture.Target.HandleAsync(fixture.Message);

            Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
            Assert.Equal(parentStatus, parent.PackageStatusKey);
            fixture.Attempts.Verify(repository => repository.CommitChangesAsync(), Times.Once);
            fixture.Orchestrator.Verify(service => service.SendMessageAsync(It.IsAny<PackageValidationMessageData>()), Times.Never);
            fixture.Notifications.Verify(service => service.SendAsync(fixture.Attempt.StagedPackageIdentity.Owner,
                It.Is<StagingPromotionArtifact>(artifact => artifact.Symbols && !artifact.Succeeded)), Times.Once);
        }

        [Fact]
        public async Task DispatchFailurePropagatesAndLeavesAcceptedPromotionRetryable()
        {
            var fixture = new Fixture();
            fixture.Orchestrator.Setup(service => service.SendMessageAsync(It.IsAny<PackageValidationMessageData>()))
                .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Target.HandleAsync(fixture.Message));

            Assert.Equal(StagedPackageStatus.Promoting, fixture.Attempt.Status);
            Assert.Equal(fixture.Message.PromotionId, fixture.Attempt.ActivePromotionId);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GroupedDispatchRequiresTheAcceptedGroupPromotion(bool active)
        {
            var fixture = new Fixture();
            fixture.Attempt.StagedPackageIdentity.StagingGroupKey = 12;
            fixture.Attempt.StagedPackageIdentity.StagingGroup = new StagingGroup
            {
                Key = 12,
                ActivePromotionId = active ? fixture.Message.PromotionId : Guid.NewGuid(),
            };

            Assert.True(await fixture.Target.HandleAsync(fixture.Message));

            var trackingId = SymbolPromotionValidationTrackingId.Create(fixture.Message.PromotionId, fixture.Attempt.Key);
            fixture.Orchestrator.Verify(service => service.SendMessageAsync(It.Is<PackageValidationMessageData>(message =>
                message.ProcessValidationSet.ValidationTrackingId == trackingId)), active ? Times.Once() : Times.Never());
        }

        [Fact]
        public async Task TerminalGroupedDispatchRedeliveryRetriesFinalizationWithoutDispatch()
        {
            var fixture = new Fixture();
            fixture.Attempt.StagedPackageIdentity.StagingGroupKey = 12;
            fixture.Attempt.StagedPackageIdentity.StagingGroup = new StagingGroup { Key = 12, ActivePromotionId = fixture.Message.PromotionId };
            fixture.Attempt.Status = StagedPackageStatus.PromotionFailed;
            fixture.Groups.SetupSequence(service => service.TryFinalizeAsync(12, fixture.Message.PromotionId))
                .ThrowsAsync(new InvalidOperationException("Finalization unavailable"))
                .Returns(Task.CompletedTask);

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Target.HandleAsync(fixture.Message));
            Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);

            Assert.True(await fixture.Target.HandleAsync(fixture.Message));

            fixture.Groups.Verify(service => service.TryFinalizeAsync(12, fixture.Message.PromotionId), Times.Exactly(2));
            fixture.Attempts.Verify(repository => repository.CommitChangesAsync(), Times.Never);
            fixture.Orchestrator.Verify(service => service.SendMessageAsync(It.IsAny<PackageValidationMessageData>()), Times.Never);
        }

        [Theory]
        [InlineData("parent")]
        [InlineData("owner")]
        public async Task GroupedRedeliveryAfterEligibilityLossLeavesCompletionToOrchestration(string scenario)
        {
            var fixture = new Fixture();
            var identity = fixture.Attempt.StagedPackageIdentity;
            identity.StagingGroupKey = 12;
            identity.StagingGroup = new StagingGroup { Key = 12, ActivePromotionId = fixture.Message.PromotionId };
            var messages = new List<PackageValidationMessageData>();
            fixture.Orchestrator.Setup(service => service.SendMessageAsync(It.IsAny<PackageValidationMessageData>()))
                .Callback<PackageValidationMessageData>(messages.Add).Returns(Task.CompletedTask);
            Assert.True(await fixture.Target.HandleAsync(fixture.Message));
            if (scenario == "parent")
            {
                identity.Package.PackageStatusKey = PackageStatus.Deleted;
            }
            else
            {
                identity.Package.PackageRegistration.Owners.Clear();
            }

            Assert.True(await fixture.Target.HandleAsync(fixture.Message));

            Assert.Equal(StagedPackageStatus.Promoting, fixture.Attempt.Status);
            Assert.Equal(fixture.Message.PromotionId, identity.StagingGroup.ActivePromotionId);
            Assert.Equal(2, messages.Count);
            Assert.Equal(messages[0].ProcessValidationSet.ValidationTrackingId, messages[1].ProcessValidationSet.ValidationTrackingId);
            fixture.Attempts.Verify(repository => repository.CommitChangesAsync(), Times.Never);
            fixture.Groups.Verify(service => service.TryFinalizeAsync(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// Exposes host registrations without starting subscriptions or connecting to external services.
        /// </summary>
        private class JobHarness : Job
        {
            public void Configure(ContainerBuilder builder, IDictionary<string, string> settings = null)
            {
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string>()).Build();
                var services = new ServiceCollection();
                services.AddLogging();
                ConfigureJobServices(services, configuration);
                builder.Populate(services);
                ConfigureAutofacServices(builder, configuration);
            }
        }

        private class Fixture
        {
            public Fixture()
            {
                Message = StagingPromotionMessage.ForSymbolPackage(Guid.NewGuid(), 43);
                Attempt = new StagedSymbolPackage
                {
                    Key = Message.TargetKey,
                    ActivePromotionId = Message.PromotionId,
                    Status = StagedPackageStatus.Promoting,
                    SymbolPackageKey = 44,
                    SymbolPackage = new SymbolPackage { Key = 44, StatusKey = PackageStatus.Staged },
                    StagedPackageIdentity = new StagedPackageIdentity
                    {
                        OwnerKey = 7,
                        CurrentStagedSymbolPackageKey = Message.TargetKey,
                        Package = new Package
                        {
                            NormalizedVersion = "1.0.0",
                            PackageStatusKey = PackageStatus.Available,
                            PackageRegistration = new PackageRegistration { Id = "PackageA", Owners = new[] { new User { Key = 7 } }.ToList() },
                        },
                    },
                };
                Attempts.Setup(repository => repository.GetAll()).Returns(new[] { Attempt }.AsQueryable());
                Attempt.StagedPackageIdentity.Owner = Attempt.StagedPackageIdentity.Package.PackageRegistration.Owners.Single();
                Attempts.Setup(repository => repository.CommitChangesAsync()).Returns(Task.CompletedTask);
                Target = new StagedSymbolPackagePromotionMessageHandler(Attempts.Object, Groups.Object, Orchestrator.Object, Notifications.Object,
                    Mock.Of<ILogger<StagedSymbolPackagePromotionMessageHandler>>());
            }

            public StagingPromotionMessage Message { get; }

            public StagedSymbolPackage Attempt { get; }

            public Mock<IEntityRepository<StagedSymbolPackage>> Attempts { get; } = new Mock<IEntityRepository<StagedSymbolPackage>>();

            public Mock<IStagingGroupPromotionService> Groups { get; } = new Mock<IStagingGroupPromotionService>();

            public Mock<IPackageValidationEnqueuer> Orchestrator { get; } = new Mock<IPackageValidationEnqueuer>();

            public Mock<IStagingPromotionNotificationService> Notifications { get; } = new Mock<IStagingPromotionNotificationService>();

            public StagedSymbolPackagePromotionMessageHandler Target { get; }
        }
    }
}
