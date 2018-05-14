// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Gallery.CredentialExpiration.Models;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using NuGet.Jobs;
using NuGet.Services.KeyVault;
using NuGet.Services.Sql;
using NuGet.Services.Storage;

namespace Gallery.CredentialExpiration
{
    public class Job : JobBase
    {
        private readonly TimeSpan _defaultCommandTimeout = TimeSpan.FromMinutes(30);

        private readonly ConcurrentDictionary<string, DateTimeOffset> _contactedUsers = new ConcurrentDictionary<string, DateTimeOffset>();
        private readonly string _cursorFile = "cursor.json";

        private bool _whatIf = false;

        private string _galleryBrand;
        private string _galleryAccountUrl;

        private ISqlConnectionFactory _galleryDatabase;

        private string _mailFrom;
        private SmtpClient _smtpClient;

        private int _allowEmailResendAfterDays = 7;
        private int _warnDaysBeforeExpiration = 10;

        private Storage _storage;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            _whatIf = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, JobArgumentNames.WhatIf);

            var secretInjector = (ISecretInjector)serviceContainer.GetService(typeof(ISecretInjector));
            var databaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.GalleryDatabase);
            _galleryDatabase = new AzureSqlConnectionFactory(databaseConnectionString, secretInjector);

            _galleryBrand = JobConfigurationManager.GetArgument(jobArgsDictionary, MyJobArgumentNames.GalleryBrand);
            _galleryAccountUrl = JobConfigurationManager.GetArgument(jobArgsDictionary, MyJobArgumentNames.GalleryAccountUrl);

            _mailFrom = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.MailFrom);

            var smtpConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.SmtpUri);
            var smtpUri = new SmtpUri(new Uri(smtpConnectionString));
            _smtpClient = CreateSmtpClient(smtpUri);

            _warnDaysBeforeExpiration = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, MyJobArgumentNames.WarnDaysBeforeExpiration)
                ?? _warnDaysBeforeExpiration;

            _allowEmailResendAfterDays = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, MyJobArgumentNames.AllowEmailResendAfterDays)
                ?? _allowEmailResendAfterDays;

            var storageConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.DataStorageAccount);
            var storageContainerName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.ContainerName);

            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var storageFactory = new AzureStorageFactory(storageAccount, storageContainerName, LoggerFactory);
            _storage = storageFactory.Create();
        }

        public override async Task Run()
        {
            try
            {
                List<ExpiredCredentialData> expiredCredentials = null;

                // Who did we contact before?
                if (_storage.Exists(_cursorFile))
                {
                    string content = await _storage.LoadString(_storage.ResolveUri(_cursorFile), CancellationToken.None);
                    // Load from cursor
                    var contactedUsers = JsonConvert.DeserializeObject<Dictionary<string, DateTimeOffset>>(content);

                    // Clean older entries (contacted in last _allowEmailResendAfterDays)
                    var referenceDate = DateTimeOffset.UtcNow.AddDays(-1 * _allowEmailResendAfterDays);
                    foreach (var kvp in contactedUsers.Where(kvp => kvp.Value >= referenceDate))
                    {
                        _contactedUsers.AddOrUpdate(kvp.Key, kvp.Value, (s, offset) => kvp.Value);
                    }
                }

                // Connect to database
                using (var galleryConnection = await _galleryDatabase.CreateAsync())
                {
                    // Fetch credentials that expire in _warnDaysBeforeExpiration days 
                    // + the user's e-mail address
                    Logger.LogInformation("Retrieving expired credentials from Gallery database...");

                    expiredCredentials = (await galleryConnection.QueryWithRetryAsync<ExpiredCredentialData>(
                        Strings.GetExpiredCredentialsQuery,
                        param: new { DaysBeforeExpiration = _warnDaysBeforeExpiration },
                        maxRetries: 3,
                        commandTimeout: _defaultCommandTimeout)).ToList();

                    Logger.LogInformation("Retrieved {ExpiredCredentials} expired credentials.",
                        expiredCredentials.Count);
                }

                // Add default description for non-scoped API keys
                expiredCredentials
                    .Where(cred => string.IsNullOrEmpty(cred.Description))
                    .ToList()
                    .ForEach(ecd => ecd.Description = Constants.NonScopedApiKeyDescription);

                // Group credentials for each user
                var userToExpiredCredsMapping = expiredCredentials
                    .GroupBy(x => x.Username)
                    .ToDictionary(user => user.Key, value => value.ToList());

                // Handle expiring credentials
                var jobRunTime = DateTimeOffset.UtcNow;
                foreach (var userCredMapping in userToExpiredCredsMapping)
                {
                    var username = userCredMapping.Key;
                    var credentialList = userCredMapping.Value;

                    // Split credentials into two lists: Expired and Expiring to aggregate messages
                    var expiringCredentialList = credentialList
                        .Where(x => (x.Expires - jobRunTime).TotalDays > 0)
                        .ToList();
                    var expiredCredentialList = credentialList
                        .Where(x => (x.Expires - jobRunTime).TotalDays <= 0)
                        .ToList();

                    DateTimeOffset userContactTime;
                    if (!_contactedUsers.TryGetValue(username, out userContactTime))
                    {
                        // send expiring API keys email notification
                        await HandleExpiredCredentialEmail(username, expiringCredentialList, jobRunTime, expired: false);

                        // send expired API keys email notification
                        await HandleExpiredCredentialEmail(username, expiredCredentialList, jobRunTime, expired: true);
                    }
                    else
                    {
                        Logger.LogDebug("Skipping expired credential for user {Username} - already handled at {JobRuntime}.",
                            username, userContactTime);
                    }
                }
            }
            finally
            {
                // Make sure we know who has been contacted today, so they do not get double
                // e-mail notifications.
                string json = JsonConvert.SerializeObject(_contactedUsers);
                var content = new StringStorageContent(json, "application/json");
                await _storage.Save(_storage.ResolveUri(_cursorFile), content, CancellationToken.None);
            }
        }

        private async Task HandleExpiredCredentialEmail(string username, List<ExpiredCredentialData> credentialList, DateTimeOffset jobRunTime, bool expired)
        {
            if (credentialList == null || credentialList.Count == 0)
            {
                return;
            }

            Logger.LogInformation("Handling {Expired} credential(s) for user {Username} (Keys: {Descriptions})...",
                expired ? "expired" : "expiring",
                username,
                string.Join(", ", credentialList.Select(x => x.Description).ToList()));

            // Build message
            var userEmail = credentialList.FirstOrDefault().EmailAddress;
            var mailMessage = new MailMessage(_mailFrom, userEmail);

            var apiKeyExpiryMessageList = credentialList
                .Select(x => BuildApiKeyExpiryMessage(x.Description, x.Expires, jobRunTime))
                .ToList();

            var apiKeyExpiryMessage = string.Join(Environment.NewLine, apiKeyExpiryMessageList);
            // Build email body
            if (expired)
            {
                mailMessage.Subject = string.Format(Strings.ExpiredEmailSubject, _galleryBrand);
                mailMessage.Body = string.Format(Strings.ExpiredEmailBody, username, _galleryBrand, apiKeyExpiryMessage, _galleryAccountUrl);
            }
            else
            {
                mailMessage.Subject = string.Format(Strings.ExpiringEmailSubject, _galleryBrand);
                mailMessage.Body = string.Format(Strings.ExpiringEmailBody, username, _galleryBrand, apiKeyExpiryMessage, _galleryAccountUrl);
            }

            // Send email
            try
            {
                if (!_whatIf) // if WhatIf is passed, we will not send e-mails (e.g. dev/int don't have to annoy users)
                {
                    await _smtpClient.SendMailAsync(mailMessage);
                }

                Logger.LogInformation("Handled {Expired} credential for user {Username}.",
                    expired ? "expired" : "expiring",
                    username);

                _contactedUsers.AddOrUpdate(username, jobRunTime, (s, offset) => jobRunTime);
            }
            catch (SmtpFailedRecipientException ex)
            {
                var logMessage = "Failed to handle credential for user {Username} - recipient failed!";
                Logger.LogWarning(LogEvents.FailedToSendMail, ex, logMessage, username);
            }
            catch (Exception ex)
            {
                var logMessage = "Failed to handle credential for user {Username}.";
                Logger.LogCritical(LogEvents.FailedToHandleExpiredCredential, ex, logMessage, username);

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

        private SmtpClient CreateSmtpClient(SmtpUri smtpUri)
        {
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
    }
}