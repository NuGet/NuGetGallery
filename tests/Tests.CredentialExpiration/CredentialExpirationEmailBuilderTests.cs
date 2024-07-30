// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using Gallery.CredentialExpiration;
using Gallery.CredentialExpiration.Models;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace Tests.CredentialExpiration
{
    public class CredentialExpirationEmailBuilderTests
    {
        public class TheConstructor
        {
            [Fact]
            public void ThrowsIfNullInitializationConfiguration()
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new CredentialExpirationEmailBuilder(
                        null, 
                        new MailAddress("sender@s.com"), 
                        "a", 
                        CreateValidList(),
                        DateTimeOffset.MinValue, 
                        false));
            }

            [Fact]
            public void ThrowsIfNullSender()
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new CredentialExpirationEmailBuilder(
                        new InitializationConfiguration(),
                        null,
                        "a",
                        CreateValidList(),
                        DateTimeOffset.MinValue,
                        false));
            }

            [Fact]
            public void ThrowsIfNullUsername()
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new CredentialExpirationEmailBuilder(
                        new InitializationConfiguration(),
                        new MailAddress("sender@s.com"),
                        null,
                        CreateValidList(),
                        DateTimeOffset.MinValue,
                        false));
            }

            [Fact]
            public void ThrowsIfNullList()
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new CredentialExpirationEmailBuilder(
                        new InitializationConfiguration(),
                        new MailAddress("sender@s.com"),
                        "a",
                        null,
                        DateTimeOffset.MinValue,
                        false));
            }

            [Fact]
            public void ThrowsIfEmptyList()
            {
                Assert.Throws<ArgumentException>(() =>
                    new CredentialExpirationEmailBuilder(
                        new InitializationConfiguration(),
                        new MailAddress("sender@s.com"),
                        "a",
                        new List<ExpiredCredentialData>(),
                        DateTimeOffset.MinValue,
                        false));
            }

            [Fact]
            public void ThrowsIfNullEmailInList()
            {
                Assert.Throws<ArgumentException>(() =>
                    new CredentialExpirationEmailBuilder(
                        new InitializationConfiguration(),
                        new MailAddress("sender@s.com"),
                        "a",
                        new List<ExpiredCredentialData> { new ExpiredCredentialData { EmailAddress = null } },
                        DateTimeOffset.MinValue,
                        false));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void Succeeds(bool areCredentialsExpired)
            {
                var config = new InitializationConfiguration();
                var sender = new MailAddress("sender@s.com");
                var username = "a";
                var list = CreateValidList();
                var runTime = new DateTimeOffset(2018, 10, 31, 12, 10, 31, TimeSpan.Zero);

                var result = new CredentialExpirationEmailBuilder(
                    config, sender, username, list, runTime, areCredentialsExpired);

                Assert.Equal(config, result.InitializationConfiguration);
                Assert.Equal(sender, result.Sender);
                Assert.Equal(username, result.Username);
                Assert.Equal(username, result.UserAddress.DisplayName);
                Assert.Equal(list.First().EmailAddress, result.UserAddress.Address);
                Assert.Equal(list, result.Credentials);
                Assert.Equal(runTime, result.JobRunTime);
                Assert.Equal(areCredentialsExpired, result.AreCredentialsExpired);
            }

            private static List<ExpiredCredentialData> CreateValidList()
            {
                return new List<ExpiredCredentialData>
                {
                    new ExpiredCredentialData
                    {
                        EmailAddress = "a@b.com"
                    },

                    new ExpiredCredentialData
                    {
                        EmailAddress = "b@c.com"
                    }
                };
            }
        }

        public class TheGetSubjectWithExpiredCredentialsMethod
            : TheGetSubjectMethod
        {
            public TheGetSubjectWithExpiredCredentialsMethod()
                : base(true)
            {
            }
        }

        public class TheGetSubjectWithExpiringCredentialsMethod
            : TheGetSubjectMethod
        {
            public TheGetSubjectWithExpiringCredentialsMethod()
                : base(false)
            {
            }
        }

        public abstract class TheGetSubjectMethod
            : CredentialExpirationEmailBuilderMethodTest
        {
            public TheGetSubjectMethod(bool areCredentialsExpired)
                : base(areCredentialsExpired)
            {
            }

            [Fact]
            public void GetsExpectedSubject()
            {
                var result = Builder.GetSubject();

                var expected = string.Format(
                    AreCredentialsExpired ? Strings.ExpiredEmailSubject : Strings.ExpiringEmailSubject, 
                    Brand);

                Assert.Equal(expected, result);
            }
        }

        public class TheGetBodyMethodWithExpiredCredentials
            : TheGetBodyMethod
        {
            public TheGetBodyMethodWithExpiredCredentials()
                : base(true)
            {
            }
        }

        public class TheGetBodyMethodWithExpiringCredentials
            : TheGetBodyMethod
        {
            public TheGetBodyMethodWithExpiringCredentials()
                : base(false)
            {
            }
        }

        public abstract class TheGetBodyMethod
            : CredentialExpirationEmailBuilderMethodTest
        {
            public TheGetBodyMethod(bool areCredentialsExpired)
                : base(areCredentialsExpired)
            {
            }

            [Theory]
            [InlineData(EmailFormat.Html)]
            [InlineData(EmailFormat.Markdown)]
            [InlineData(EmailFormat.PlainText)]
            public void GetsExpectedBody(EmailFormat format)
            {
                var result = Builder.GetBody(format);

                var intro = AreCredentialsExpired
                    ? $"We wanted to inform you that the following API key(s) on {Brand} have expired:"
                    : $"We wanted to inform you that the following API key(s) on {Brand} will expire soon:";

                Assert.Contains(
                    intro,
                    result);

                var visitStatement = AreCredentialsExpired
                    ? $"Visit {Url} to generate a new API key(s) so that you can continue pushing packages."
                    : $"Visit {Url} to generate a new API key(s) so that you can continue pushing packages using them.";

                Assert.Contains(
                    visitStatement,
                    result);

                foreach (var credential in new ExpiredCredentialData[] { FirstCredential, SecondCredential })
                {
                    var credentialMessage = AreCredentialsExpired
                        ? $"{credential.Description} - has expired."
                        : $"{credential.Description} - expires in {(int)(credential.Expires - JobRunTime).TotalDays} day(s).";

                    Assert.Contains(
                        credentialMessage,
                        result);
                }
            }
        }

        public class CredentialExpirationEmailBuilderMethodTest
        {
            public const string Brand = "my cool nugget service";
            public const string Url = "ftp://nuggets.gov";

            public InitializationConfiguration Config = 
                new InitializationConfiguration { GalleryBrand = Brand, GalleryAccountUrl = Url };

            public const string Username = "username";
            public const string Email = "user@nuggets.gov";

            public ExpiredCredentialData FirstCredential { get; }
            public ExpiredCredentialData SecondCredential { get; }

            public static DateTimeOffset JobRunTime = 
                new DateTimeOffset(2018, 10, 31, 12, 10, 31, TimeSpan.Zero);

            public bool AreCredentialsExpired { get; }

            public CredentialExpirationEmailBuilder Builder { get; }

            public CredentialExpirationEmailBuilderMethodTest(bool areCredentialsExpired)
            {
                FirstCredential = new ExpiredCredentialData
                {
                    Username = Username,
                    EmailAddress = Email,
                    Description = "first",
                    Expires = JobRunTime - new TimeSpan(25 * (areCredentialsExpired ? 1 : -1), 0, 0)
                };

                SecondCredential = new ExpiredCredentialData
                {
                    Username = Username,
                    EmailAddress = Email,
                    Description = "second",
                    Expires = JobRunTime - new TimeSpan(2 * 25 * (areCredentialsExpired ? 1 : -1), 0, 0)
                };

                AreCredentialsExpired = areCredentialsExpired;

                Builder = new CredentialExpirationEmailBuilder(
                    Config,
                    new MailAddress("sender@s.com"),
                    Username,
                    new List<ExpiredCredentialData> { FirstCredential, SecondCredential },
                    JobRunTime,
                    areCredentialsExpired);
            }
        }
    }
}
