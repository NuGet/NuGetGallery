// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Net;
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
using NuGet.Services.Storage;

namespace Gallery.CredentialExpiration
{
    public class Job : JsonConfigurationJob
    {
        private readonly TimeSpan _defaultCommandTimeout = TimeSpan.FromMinutes(30);

        private readonly string _cursorFile = "cursorv2.json";

        private InitializationConfiguration Configuration { get; set; }

        private Storage Storage { get; set; }

        private SmtpClient SmtpClient { get; set; }

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            Configuration = _serviceProvider.GetRequiredService<IOptionsSnapshot<InitializationConfiguration>>().Value;

            SmtpClient = CreateSmtpClient(Configuration.SmtpUri);
            
            var storageAccount = CloudStorageAccount.Parse(Configuration.DataStorageAccount);
            var storageFactory = new AzureStorageFactory(storageAccount, Configuration.ContainerName, LoggerFactory);
            Storage = storageFactory.Create();
        }

        public override async Task Run()
        {
            var jobRunTime = DateTimeOffset.UtcNow;
            // Default values
            var jobCursor = new JobRunTimeCursor( jobCursorTime: jobRunTime, maxProcessedCredentialsTime: jobRunTime );
            var galleryCredentialExpiration = new GalleryCredentialExpiration(this,
                new CredentialExpirationJobMetadata(jobRunTime, Configuration.WarnDaysBeforeExpiration, jobCursor));

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
                        new CredentialExpirationJobMetadata(jobRunTime, Configuration.WarnDaysBeforeExpiration, jobCursor));
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
                var userToExpiredCredsMapping = credentialsInRange
                    .GroupBy(x => x.Username)
                    .ToDictionary(user => user.Key, value => value.ToList());

                foreach (var userCredMapping in userToExpiredCredsMapping)
                {
                    var username = userCredMapping.Key;
                    var credentialList = userCredMapping.Value;

                    // Split credentials into two lists: Expired and Expiring to aggregate messages
                    var expiringCredentialList = galleryCredentialExpiration.GetExpiringCredentials(credentialList);
                    var expiredCredentialList = galleryCredentialExpiration.GetExpiredCredentials(credentialList);

                    await HandleExpiredCredentialEmail(username, expiringCredentialList, jobRunTime, expired: false);

                    // send expired API keys email notification
                    await HandleExpiredCredentialEmail(username, expiredCredentialList, jobRunTime, expired: true);
                }
            }
            finally
            {
                JobRunTimeCursor newCursor = new JobRunTimeCursor(
                    jobCursorTime: jobRunTime,
                    maxProcessedCredentialsTime: galleryCredentialExpiration.GetMaxNotificationDate());

                string json = JsonConvert.SerializeObject(newCursor);
                var content = new StringStorageContent(json, "application/json");
                await Storage.Save(Storage.ResolveUri(_cursorFile), content, CancellationToken.None);
            }
        }

        private async Task HandleExpiredCredentialEmail(string username, List<ExpiredCredentialData> credentialList, DateTimeOffset jobRunTime, bool expired)
        {
            if (credentialList == null || credentialList.Count == 0)
            {
                return;
            }

            Logger.LogInformation("Handling {Expired} credential(s) (Keys: {Descriptions})...",
                expired ? "expired" : "expiring",
                string.Join(", ", credentialList.Select(x => x.Description).ToList()));

            // Build message
            var userEmail = credentialList.FirstOrDefault().EmailAddress;
            var mailMessage = new MailMessage(Configuration.MailFrom, userEmail);

            var apiKeyExpiryMessageList = credentialList
                .Select(x => BuildApiKeyExpiryMessage(x.Description, x.Expires, jobRunTime))
                .ToList();

            var apiKeyExpiryMessage = string.Join(Environment.NewLine, apiKeyExpiryMessageList);
            // Build email body
            if (expired)
            {
                mailMessage.Subject = string.Format(Strings.ExpiredEmailSubject, Configuration.GalleryBrand);
                mailMessage.Body = string.Format(Strings.ExpiredEmailBody, username, Configuration.GalleryBrand, apiKeyExpiryMessage, Configuration.GalleryAccountUrl);
            }
            else
            {
                mailMessage.Subject = string.Format(Strings.ExpiringEmailSubject, Configuration.GalleryBrand);
                mailMessage.Body = string.Format(Strings.ExpiringEmailBody, username, Configuration.GalleryBrand, apiKeyExpiryMessage, Configuration.GalleryAccountUrl);
            }

            // Send email
            try
            {
                if (!Configuration.WhatIf) // if WhatIf is passed, we will not send e-mails (e.g. dev/int don't have to annoy users)
                {
                    await SmtpClient.SendMailAsync(mailMessage);
                }

                Logger.LogInformation("Handled {Expired} credential .",
                    expired ? "expired" : "expiring");
            }
            catch (SmtpFailedRecipientException ex)
            {
                var logMessage = "Failed to handle credential - recipient failed!";
                Logger.LogWarning(LogEvents.FailedToSendMail, ex, logMessage);
            }
            catch (Exception ex)
            {
                var logMessage = "Failed to handle credential .";
                Logger.LogCritical(LogEvents.FailedToHandleExpiredCredential, ex, logMessage);

                throw;
            }
        }

        private static string BuildApiKeyExpiryMessage(string description, DateTimeOffset expiry, DateTimeOffset currentTime)
        {
            var expiryInDays = (expiry - currentTime).TotalDays;
            var message = expiryInDays < 0
                ? string.Format(Strings.ApiKeyExpired, description)
                : string.Format(Strings.ApiKeyExpiring, description, (int)expiryInDays);

            // \u2022 - Unicode for bullet point.
            return "\u2022 " + message + Environment.NewLine;
        }
        
        private SmtpClient CreateSmtpClient(string smtpUriString)
        {
            var smtpUri = new SmtpUri(new Uri(smtpUriString));
            var smtpClient = new SmtpClient(smtpUri.Host, smtpUri.Port)
            {
                EnableSsl = smtpUri.Secure
            };

            if (!string.IsNullOrWhiteSpace(smtpUri.UserName))
            {
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new NetworkCredential(
                    smtpUri.UserName,
                    smtpUri.Password);
            }

            return smtpClient;
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