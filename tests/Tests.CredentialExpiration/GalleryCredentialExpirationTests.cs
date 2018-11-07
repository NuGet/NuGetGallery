// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using Gallery.CredentialExpiration;
using Gallery.CredentialExpiration.Models;
using Xunit;


namespace Tests.CredentialExpiration
{
    public class GalleryCredentialExpirationTests
    {

        [Fact]
        public void ExpiringCredentialsAreCorrectReturned()
        {
            // Arange
            var cursorTime = new DateTimeOffset(year: 2018, month: 4, day: 10, hour: 8, minute: 2, second: 2, offset: TimeSpan.FromSeconds(0));
            var jobRunTime = cursorTime.AddDays(2);
            var maxProcessedCredentialTime = cursorTime.AddDays(1);
            int warnDaysBeforeExpiration = 2;
            var jobMetadata = new CredentialExpirationJobMetadata(jobRunTime, warnDaysBeforeExpiration, new JobRunTimeCursor(cursorTime, cursorTime.AddDays(1)));
            var expiringCred1 = new ExpiredCredentialData()
            {
                Expires = jobRunTime.AddDays(warnDaysBeforeExpiration).AddHours(-1)
            };
            var expiringCred2 = new ExpiredCredentialData()
            {
                Expires = jobRunTime.AddDays(warnDaysBeforeExpiration)
            };
            var expiringOldCred = new ExpiredCredentialData()
            {
                // Time is smaller than the max of the credentials' time in the cursor
                Expires = maxProcessedCredentialTime.AddHours(-1)
            };
            var credentialSet = new List<ExpiredCredentialData> { expiringCred1, expiringCred2, expiringOldCred};
            var testCredentials = new TestCredentialExpiration(jobMetadata, credentialSet);

            // Act
            var expiringCred = testCredentials.GetExpiringCredentials(credentialSet).OrderBy(c => c.Expires).ToList();

            // Assert
            Assert.Equal(2, testCredentials.GetExpiringCredentials(credentialSet).Count);
            Assert.Equal(expiringCred1.Expires, expiringCred[0].Expires);
            Assert.Equal(expiringCred2.Expires, expiringCred[1].Expires);
        }


        [Fact]
        public void ExpiredCredentialsAreCorrectReturned()
        {
            // Arange
            var cursorTime = new DateTimeOffset(year: 2018, month: 4, day: 10, hour: 8, minute: 2, second: 2, offset: TimeSpan.FromSeconds(0));
            var jobRunTime = cursorTime.AddDays(2);
            var maxProcessedCredentialTime = cursorTime.AddDays(1);
            int warnDaysBeforeExpiration = 2;
            var jobMetadata = new CredentialExpirationJobMetadata(jobRunTime, warnDaysBeforeExpiration, new JobRunTimeCursor(cursorTime, cursorTime.AddDays(1)));
            var expiredCredential = new ExpiredCredentialData()
            {
                Expires = jobRunTime.AddHours(-1)
            };
            var notExpiredCredential1 = new ExpiredCredentialData()
            {
                Expires = jobRunTime
            };
            var notExpiredCredenatial2 = new ExpiredCredentialData()
            {
                Expires = jobRunTime.AddHours(1)
            };
            var credentialSet = new List<ExpiredCredentialData> { expiredCredential, notExpiredCredential1, notExpiredCredenatial2 };
            var testCredentials = new TestCredentialExpiration(jobMetadata, credentialSet);

            // Act
            var expiredCredentials = testCredentials.GetExpiredCredentials(credentialSet).OrderBy(c => c.Expires).ToList();

            // Assert 
            Assert.Single(expiredCredentials);
            Assert.Equal(expiredCredential.Expires, expiredCredentials[0].Expires);
        }


        [Fact]
        public void TwoJobsInSameDayShouldNotSendDuplicateExpiringEmails()
        {
            // Arange
            var cursorTime = new DateTimeOffset(year: 2018, month: 4, day: 10, hour: 8, minute: 2, second: 2, offset: TimeSpan.FromSeconds(0));
            var jobRunTime = cursorTime.AddHours(1);
            int warnDaysBeforeExpiration = 2;
            var maxProcessedCredentialTime = cursorTime.AddDays(warnDaysBeforeExpiration);
            var jobMetadata = new CredentialExpirationJobMetadata(jobRunTime, warnDaysBeforeExpiration, new JobRunTimeCursor(cursorTime, maxProcessedCredentialTime));
            var previouslyProcessedCred = new ExpiredCredentialData()
            {
                Expires = maxProcessedCredentialTime
            };
            var expiringCred = new ExpiredCredentialData()
            {
                // A datetime <= jobRunTime.AddDays(warnDaysBeforeExpiration) and larger than maxProcessedCredentialTime
                Expires = jobRunTime.AddDays(warnDaysBeforeExpiration).AddMinutes(-1)
            };
            var credentialSet = new List<ExpiredCredentialData> { previouslyProcessedCred, expiringCred };
            var testCredentials = new TestCredentialExpiration(jobMetadata, credentialSet);

            // Act
            var expiringCredentials = testCredentials.GetExpiringCredentials(credentialSet).OrderBy(c => c.Expires).ToList();

            // Assert
            Assert.Single(expiringCredentials);
            Assert.Equal(expiringCred.Expires, expiringCredentials[0].Expires);
        }

        [Fact]
        public void OutdatedCursorShouldNotSendExpiringToExpiredEmails()
        {
            // Arange
            var cursorTime = new DateTimeOffset(year: 2018, month: 4, day: 10, hour: 8, minute: 2, second: 2, offset: TimeSpan.FromSeconds(0));
            var jobRunTime = cursorTime.AddDays(2);
            int warnDaysBeforeExpiration = 2;
            var maxProcessedCredentialTime = jobRunTime.AddDays(-1);
            var jobMetadata = new CredentialExpirationJobMetadata(jobRunTime, warnDaysBeforeExpiration, new JobRunTimeCursor(cursorTime, maxProcessedCredentialTime));

            var expiredCred = new ExpiredCredentialData()
            {
                Expires = maxProcessedCredentialTime.AddHours(1)
            };
            var expiringCred = new ExpiredCredentialData()
            {
                // A datetime <= jobRunTime.AddDays(warnDaysBeforeExpiration) and larger than maxProcessedCredentialTime
                Expires = jobRunTime.AddDays(warnDaysBeforeExpiration).AddMinutes(-1)
            };
            var credentialSet = new List<ExpiredCredentialData> { expiredCred, expiringCred };
            var testCredentials = new TestCredentialExpiration(jobMetadata, credentialSet);

            // Act
            var expiringCredentials = testCredentials.GetExpiringCredentials(credentialSet).OrderBy(c => c.Expires).ToList();
            var expiredCredentials = testCredentials.GetExpiredCredentials(credentialSet).OrderBy(c => c.Expires).ToList();

            // Assert
            Assert.Single(expiringCredentials);
            Assert.Equal(expiringCred.Expires, expiringCredentials[0].Expires);
            Assert.Single(expiredCredentials);
            Assert.Equal(expiredCred.Expires, expiredCredentials[0].Expires);
        }

        [Fact]
        public void GetMaxAndGetMinAreAsExpected()
        {
            // Arange
            var cursorTime = new DateTimeOffset(year: 2018, month: 4, day: 10, hour: 8, minute: 2, second: 2, offset: TimeSpan.FromSeconds(0));
            var jobRunTime = cursorTime.AddHours(1);
            int warnDaysBeforeExpiration = 2;
            var maxProcessedCredentialTime = cursorTime.AddDays(warnDaysBeforeExpiration);
            var jobMetadata = new CredentialExpirationJobMetadata(jobRunTime, warnDaysBeforeExpiration, new JobRunTimeCursor(cursorTime, maxProcessedCredentialTime));
            var testCredentials = new TestCredentialExpiration(jobMetadata, null);

            // Act
            var minValue = testCredentials.GetMinNotificationDate();
            var maxValue = testCredentials.GetMaxNotificationDate();

            // Assert
            Assert.Equal(cursorTime, minValue);
            Assert.Equal(jobRunTime.AddDays(warnDaysBeforeExpiration), maxValue);
        }
    }
}
