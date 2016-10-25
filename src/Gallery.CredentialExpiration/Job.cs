// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Gallery.CredentialExpiration.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Jobs;
using NuGet.Services.Configuration;
using NuGet.Services.Logging;

namespace Gallery.CredentialExpiration
{
    public class Job : JobBase
    {
        private const int DefaultCommandTimeout = 1800; // 30 minutes max

        private readonly ConcurrentDictionary<string, DateTimeOffset> _contactedUsers = new ConcurrentDictionary<string, DateTimeOffset>();
        private readonly string _cursorFile = "cursor.json";

        private bool _whatIf = false;

        private string _galleryBrand;
        private string _galleryAccountUrl;

        private SqlConnectionStringBuilder _galleryDatabase;

        private string _mailFrom;
        private SmtpClient _smtpClient;

        private int _warnDaysBeforeExpiration = 10;
        
        private ILogger _logger;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = jobArgsDictionary.GetOrNull(JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(ConsoleLogOnly);
                var loggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration);
                _logger = loggerFactory.CreateLogger<Job>();

                _whatIf = jobArgsDictionary.GetOrNull<bool>(JobArgumentNames.WhatIf) ?? false;

                var databaseConnectionString = jobArgsDictionary[JobArgumentNames.GalleryDatabase];
                _galleryDatabase = new SqlConnectionStringBuilder(databaseConnectionString);

                _galleryBrand = jobArgsDictionary[MyJobArgumentNames.GalleryBrand];
                _galleryAccountUrl = jobArgsDictionary[MyJobArgumentNames.GalleryAccountUrl];

                _mailFrom = jobArgsDictionary[JobArgumentNames.MailFrom];

                var smtpConnectionString = jobArgsDictionary[JobArgumentNames.SmtpUri];
                var smtpUri = new SmtpUri(new Uri(smtpConnectionString));
                _smtpClient = CreateSmtpClient(smtpUri);

                var temp = jobArgsDictionary.GetOrNull<int>(MyJobArgumentNames.WarnDaysBeforeExpiration);
                if (temp.HasValue)
                {
                    _warnDaysBeforeExpiration = temp.Value;
                }
            }
            catch (Exception exception)
            {
                _logger.LogCritical("Failed to initialize job! {Exception}", exception);

                return false;
            }

            return true;
        }

        public override async Task<bool> Run()
        {
            try
            {
                List<ExpiredCredentialData> expiredCredentials = null;

                // Who did we contact before?
                if (File.Exists(_cursorFile))
                {
                    // Load from cursor
                    var contactedUsers = JsonConvert.DeserializeObject<Dictionary<string, DateTimeOffset>>(
                        File.ReadAllText(_cursorFile));

                    // Clean older entries (contacted in last _warnDaysBeforeExpiration * 2 days)
                    var referenceDate = DateTimeOffset.UtcNow.AddDays(-2 * _warnDaysBeforeExpiration);
                    foreach (var kvp in contactedUsers.Where(kvp => kvp.Value >= referenceDate))
                    {
                        _contactedUsers.AddOrUpdate(kvp.Key, kvp.Value, (s, offset) => kvp.Value);
                    }
                }

                // Connect to database
                using (var galleryConnection = await _galleryDatabase.ConnectTo())
                {
                    // Fetch credentials that expire in _warnDaysBeforeExpiration days 
                    // + the user's e-mail address
                    _logger.LogInformation("Retrieving expired credentials from {InitialCatalog}...",
                        _galleryDatabase.InitialCatalog);

                    expiredCredentials = (await galleryConnection.QueryWithRetryAsync<ExpiredCredentialData>(
                        string.Format(Strings.GetExpiredCredentialsQuery, _warnDaysBeforeExpiration),
                        maxRetries: 3,
                        commandTimeout: DefaultCommandTimeout)).ToList();

                    _logger.LogInformation("Retrieved {ExpiredCredentials} expired credentials.",
                        expiredCredentials.Count);
                }

                // Handle expiring credentials
                var jobRunTime = DateTimeOffset.UtcNow;
                foreach (var expiredCredential in expiredCredentials)
                {
                    if (!_contactedUsers.ContainsKey(expiredCredential.Username))
                    {
                        await HandleExpiredCredentialEmail(expiredCredential, jobRunTime);
                    }
                    else
                    {
                        _logger.LogDebug("Skipping expired credential for user {Username} - already handled today.",
                            expiredCredential.Username);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Job run failed! {Exception}", ex);

                return false;
            }
            finally
            {
                // Make sure we know who has been contacted today, so they do not get double
                // e-mail notifications.
                File.WriteAllText(_cursorFile, JsonConvert.SerializeObject(_contactedUsers));
            }

            return true;
        }

        private async Task HandleExpiredCredentialEmail(ExpiredCredentialData expiredCredential, DateTimeOffset jobRunTime)
        {
            _logger.LogInformation("Handling expired credential for user {Username} (expires: {Expires})...", expiredCredential.Username, expiredCredential.Expires);

            // Build message
            var mailMessage = new MailMessage(_mailFrom, expiredCredential.EmailAddress);

            // Build email body
            var expiresInDays = expiredCredential.Expires.UtcDateTime - DateTime.UtcNow;
            if (expiresInDays.TotalDays <= 0)
            {
                mailMessage.Subject = string.Format(Strings.ExpiredEmailSubject, _galleryBrand);
                mailMessage.Body = string.Format(Strings.ExpiredEmailBody, _galleryBrand, _galleryAccountUrl);
            }
            else
            {
                mailMessage.Subject = string.Format(Strings.ExpiringEmailSubject, _galleryBrand);
                mailMessage.Body = string.Format(Strings.ExpiringEmailBody, _galleryBrand, _galleryAccountUrl, (int)expiresInDays.TotalDays);
            }

            // Send email
            try
            {
                if (!_whatIf) // if WhatIf is passed, we will not send e-mails (e.g. dev/int don't have to annoy users)
                {
                    await _smtpClient.SendMailAsync(mailMessage);
                }

                _logger.LogInformation("Handled expired credential for user {Username}.", expiredCredential.Username);

                _contactedUsers.AddOrUpdate(expiredCredential.Username, jobRunTime, (s, offset) => jobRunTime);
            }
            catch (SmtpFailedRecipientException ex)
            {
                _logger.LogWarning("Failed to handle expired credential for user {Username} - recipient failed.", expiredCredential.Username, ex);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Failed to handle expired credential for user {Username}.", expiredCredential.Username, ex);

                throw;
            }
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