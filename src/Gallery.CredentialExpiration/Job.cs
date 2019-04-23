// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Gallery.CredentialExpiration.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using NuGet.Jobs;
using NuGet.Services.Messaging;
using NuGet.Services.Messaging.Email;
using NuGet.Services.ServiceBus;
using NuGet.Services.Storage;

namespace Gallery.CredentialExpiration
{
    public class Job : JsonConfigurationJob
    {
        private readonly TimeSpan _defaultCommandTimeout = TimeSpan.FromMinutes(30);

        private readonly string _cursorFile = "cursorv2.json";

        private InitializationConfiguration InitializationConfiguration { get; set; }
        private MailAddress FromAddress { get; set; }
        private AsynchronousEmailMessageService EmailService { get; set; }

        private Storage Storage { get; set; }

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            InitializationConfiguration = _serviceProvider.GetRequiredService<IOptionsSnapshot<InitializationConfiguration>>().Value;

            var serializer = new ServiceBusMessageSerializer();
            var topicClient = new TopicClientWrapper(InitializationConfiguration.EmailPublisherConnectionString, InitializationConfiguration.EmailPublisherTopicName);
            var enqueuer = new EmailMessageEnqueuer(topicClient, serializer, LoggerFactory.CreateLogger<EmailMessageEnqueuer>());
            EmailService = new AsynchronousEmailMessageService(
                enqueuer,
                LoggerFactory.CreateLogger<AsynchronousEmailMessageService>(),
                InitializationConfiguration);

            FromAddress = new MailAddress(InitializationConfiguration.MailFrom);
            
            var storageAccount = CloudStorageAccount.Parse(InitializationConfiguration.DataStorageAccount);
            var storageFactory = new AzureStorageFactory(
                storageAccount,
                InitializationConfiguration.ContainerName,
                LoggerFactory.CreateLogger<AzureStorage>());
            Storage = storageFactory.Create();
        }

        public override async Task Run()
        {
            var jobRunTime = DateTimeOffset.UtcNow;
            // Default values
            var jobCursor = new JobRunTimeCursor( jobCursorTime: jobRunTime, maxProcessedCredentialsTime: jobRunTime );
            var galleryCredentialExpiration = new GalleryCredentialExpiration(this,
                new CredentialExpirationJobMetadata(jobRunTime, InitializationConfiguration.WarnDaysBeforeExpiration, jobCursor));

            try
            {
                List<ExpiredCredentialData> credentialsInRange = null;

                // Get the most recent date for the emails being sent 
                if (Storage.Exists(_cursorFile))
                {
                    string content = await Storage.LoadString(Storage.ResolveUri(_cursorFile), CancellationToken.None);
                    // Load from cursor
                    // Throw if the schema is not correct to ensure that not-intended emails are sent.
                    jobCursor = JsonConvert.DeserializeObject<JobRunTimeCursor>(
                        content,
                        new JsonSerializerSettings() { MissingMemberHandling = MissingMemberHandling.Error });

                    galleryCredentialExpiration = new GalleryCredentialExpiration(this,
                        new CredentialExpirationJobMetadata(jobRunTime, InitializationConfiguration.WarnDaysBeforeExpiration, jobCursor));
                }

                // Connect to database
                Logger.LogInformation("Retrieving expired credentials from Gallery database...");
                credentialsInRange = await galleryCredentialExpiration.GetCredentialsAsync(_defaultCommandTimeout);
                Logger.LogInformation("Retrieved {ExpiredCredentials} expired credentials.",
                       credentialsInRange.Count);

                // Add default description for non-scoped API keys
                credentialsInRange
                    .Where(cred => string.IsNullOrEmpty(cred.Description))
                    .ToList()
                    .ForEach(ecd => ecd.Description = Constants.NonScopedApiKeyDescription);

                // Group credentials for each user
                var userToCredentialsMapping = credentialsInRange
                    .GroupBy(x => x.Username)
                    .ToDictionary(user => user.Key, value => value.ToList());

                foreach (var userCredMapping in userToCredentialsMapping)
                {
                    var username = userCredMapping.Key;
                    var credentialList = userCredMapping.Value;

                    // Split credentials into two lists: Expired and Expiring to aggregate messages
                    var expiringCredentialList = galleryCredentialExpiration.GetExpiringCredentials(credentialList);
                    var expiredCredentialList = galleryCredentialExpiration.GetExpiredCredentials(credentialList);

                    await HandleExpiredCredentialEmail(username, expiringCredentialList, jobRunTime, areCredentialsExpired: false);

                    // send expired API keys email notification
                    await HandleExpiredCredentialEmail(username, expiredCredentialList, jobRunTime, areCredentialsExpired: true);
                }
            }
            finally
            {
                JobRunTimeCursor newCursor = new JobRunTimeCursor(
                    jobCursorTime: jobRunTime,
                    maxProcessedCredentialsTime: galleryCredentialExpiration.GetMaxNotificationDate());

                string json = JsonConvert.SerializeObject(newCursor);
                var content = new StringStorageContent(json, "application/json");
                await Storage.Save(
                    Storage.ResolveUri(_cursorFile),
                    content,
                    overwrite: true,
                    cancellationToken: CancellationToken.None);
            }
        }

        private async Task HandleExpiredCredentialEmail(string username, List<ExpiredCredentialData> credentials, DateTimeOffset jobRunTime, bool areCredentialsExpired)
        {
            if (credentials == null || credentials.Count == 0)
            {
                return;
            }

            Logger.LogInformation("Handling {Expired} credential(s) (Keys: {Descriptions})...",
                areCredentialsExpired ? "expired" : "expiring",
                string.Join(", ", credentials.Select(x => x.Description).ToList()));

            var emailBuilder = new CredentialExpirationEmailBuilder(
                InitializationConfiguration, 
                FromAddress, 
                username, 
                credentials, 
                jobRunTime, 
                areCredentialsExpired);

            // Send email
            try
            {
                if (!InitializationConfiguration.WhatIf) // if WhatIf is passed, we will not send e-mails (e.g. dev/int don't have to annoy users)
                {
                    await EmailService.SendMessageAsync(emailBuilder);
                }

                Logger.LogInformation("Handled {Expired} credential .",
                    areCredentialsExpired ? "expired" : "expiring");
            }
            catch (Exception ex)
            {
                var logMessage = "Failed to handle credential .";
                Logger.LogCritical(LogEvents.FailedToHandleExpiredCredential, ex, logMessage);

                throw;
            }
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<InitializationConfiguration>(services, configurationRoot);
        }
    }
}