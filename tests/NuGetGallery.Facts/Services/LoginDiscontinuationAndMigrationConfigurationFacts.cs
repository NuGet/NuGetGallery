// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using System.Collections.Generic;
using Xunit;

namespace NuGetGallery.Services
{
    public class LoginDiscontinuationAndMigrationConfigurationFacts
    {
        public class TheIsLoginDiscontinuedMethod : TestContainer
        {
            private const string _incorrectDomain = "notExample.com";
            private const string _domain = "example.com";
            private const string _incorrectEmail = "fake@notExample.com";
            private const string _email = "test@example.com";

            public static IEnumerable<object[]> CredentialPasswordTypes
            {
                get
                {
                    foreach (var credentialPasswordType in new[] { CredentialTypes.Password.Pbkdf2, CredentialTypes.Password.Sha1, CredentialTypes.Password.V3 })
                    {
                        yield return MemberDataHelper.AsData(credentialPasswordType);
                    }
                }
            }

            [Theory]
            [MemberData(nameof(CredentialPasswordTypes))]
            public void IfPasswordLoginAndUserIsOnListReturnsTrue(string credentialPasswordType)
            {
                TestIsLoginDiscontinued(credentialPasswordType, isOnDomainList: true, isOnExceptionList: false, expectedResult: true);
            }

            [Theory]
            [MemberData(nameof(CredentialPasswordTypes))]
            public void IfPasswordLoginAndUserIsNotOnListReturnsFalse(string credentialPasswordType)
            {
                TestIsLoginDiscontinued(credentialPasswordType, isOnDomainList: false, isOnExceptionList: false, expectedResult: false);
            }

            [Theory]
            [MemberData(nameof(CredentialPasswordTypes))]
            public void IfPasswordLoginAndUserIsOnListWithExceptionReturnsFalse(string credentialPasswordType)
            {
                TestIsLoginDiscontinued(credentialPasswordType, isOnDomainList: true, isOnExceptionList: true, expectedResult: false);
            }

            public static IEnumerable<object[]> IfNotPasswordLoginReturnsFalse_Data
            {
                get
                {
                    foreach (var credentialType in new[] {
                        CredentialTypes.ApiKey.V1,
                        CredentialTypes.ApiKey.V2,
                        CredentialTypes.ApiKey.V3,
                        CredentialTypes.ApiKey.V4,
                        CredentialTypes.ApiKey.VerifyV1,
                        CredentialTypes.External.MicrosoftAccount,
                        CredentialTypes.External.AzureActiveDirectoryAccount })
                    {
                        foreach (var isOnDomainList in new[] { true, false })
                        {
                            foreach (var isOnExceptionList in new[] { true, false })
                            {
                                yield return MemberDataHelper.AsData(credentialType, isOnDomainList, isOnExceptionList);
                            }
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(IfNotPasswordLoginReturnsFalse_Data))]
            public void IfNotPasswordLoginReturnsFalse(string credentialType, bool isOnDomainList, bool isOnExceptionList)
            {
                TestIsLoginDiscontinued(credentialType, isOnDomainList, isOnExceptionList, expectedResult: false);
            }

            private void TestIsLoginDiscontinued(string credentialType, bool isOnDomainList, bool isOnExceptionList, bool expectedResult)
            {
                // Arrange
                var credential = new Credential(credentialType, "value");
                var user = new User("test") { EmailAddress = _email, Credentials = new[] { credential } };
                var authUser = new AuthenticatedUser(user, credential);

                var domains = isOnDomainList ? new[] { _domain } : new[] { _incorrectDomain };
                var exceptions = isOnExceptionList ? new[] { _email } : new[] { _incorrectEmail };

                var config = new LoginDiscontinuationAndMigrationConfiguration(domains, exceptions);

                // Act
                var result = config.IsLoginDiscontinued(authUser);

                // Assert
                Assert.Equal(expectedResult, result);
            }
        }
    }
}
