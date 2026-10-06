// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Gallery.Maintenance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGet.Services.ServiceBus;
using NuGetGallery.Infrastructure.Mail.Messages;
using Xunit;

namespace Tests.Gallery.Maintenance
{
    public class GalleryMaintenanceJobTests
    {
        [Fact]
        public void GetMaintenanceTasks_CreatesTasksAndDoesNotThrow()
        {
            var job = CreateJob();

            var tasks = job.GetMaintenanceTasks();

            Assert.NotEmpty(tasks);
        }

        [Fact]
        public async Task Main_ReturnsFailureWhenConfigurationIsMissing()
        {
            var path = Path.Combine(Path.GetTempPath(), $"gallery-maintenance-{Guid.NewGuid():N}.json");

            var exitCode = await Program.Main(new[] { "-Configuration", path, "-Once" });

            Assert.NotEqual(0, exitCode);
        }

        [Fact]
        public async Task SendsExpirationEmailThroughConfiguredMaintenanceTopic()
        {
            IBrokeredMessage sent = null;
            var topic = new Mock<ITopicClient>();
            topic.Setup(client => client.SendAsync(It.IsAny<IBrokeredMessage>()))
                .Callback<IBrokeredMessage>(message => sent = message).Returns(Task.CompletedTask);
            var builder = new ContainerBuilder();
            new JobHarness().Configure(builder);
            builder.RegisterInstance(topic.Object).Keyed<ITopicClient>("EmailTopic");
            using (var container = builder.Build())
            {
                var email = container.Resolve<IOptionsSnapshot<MaintenanceEmailConfiguration>>().Value;
                var owner = new User("owner") { EmailAddress = "owner@example.test", NotifyPackageStaged = true };
                await container.Resolve<IMessageService>().SendMessageAsync(new StagingExpirationMessage(email, owner,
                    "staged package PackageA 1.0.0", DateTime.UtcNow, false, email.ManagePackagesUrl, email.EmailSettingsUrl));

                Assert.NotNull(sent);
                var received = new Mock<IReceivedBrokeredMessage>();
                received.Setup(message => message.GetBody()).Returns(sent.GetBody());
                received.SetupGet(message => message.Properties).Returns(new Dictionary<string, object>(sent.Properties));
                var messageData = container.Resolve<NuGet.Services.Messaging.IServiceBusMessageSerializer>().DeserializeEmailMessageData(received.Object);
                Assert.Equal(owner.EmailAddress, Assert.Single(messageData.To));
                Assert.Contains("expires soon", messageData.Subject);
                Assert.Contains("PackageA 1.0.0", messageData.PlainTextBody);
                Assert.Contains(email.ManagePackagesUrl, messageData.PlainTextBody);
                topic.Verify(client => client.SendAsync(It.IsAny<IBrokeredMessage>()), Times.Once);
            }
        }

        /// <summary>
        /// Exposes maintenance email registrations without opening external connections.
        /// </summary>
        private class JobHarness : Job
        {
            public void Configure(ContainerBuilder builder)
            {
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Email:ManagePackagesUrl"] = "https://gallery.test/account/Packages",
                    ["Email:EmailSettingsUrl"] = "https://gallery.test/account",
                }).Build();
                var services = new ServiceCollection();
                services.AddLogging();
                ConfigureJobServices(services, configuration);
                builder.Populate(services);
                ConfigureAutofacServices(builder, configuration);
            }
        }

        private Job CreateJob()
        {
            var job = new Job();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<Job>();
            job.SetLogger(loggerFactory, logger);
            return job;
        }
    }
}
