﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using System.Collections.Generic;
using Xunit;

namespace NuGetGallery.Services
{
    public class LoginDiscontinuationConfigurationFacts
    {
        private const string _incorrectEmail = "incorrect@notExample.com";
        private const string _incorrectDomain = "notExample.com";
        private const string _domain = "example.com";
        private const string _incorrectException = "fake@notExample.com";
        private const string _email = "test@example.com";

        public static IEnumerable<object[]> PossibleListStates
        {
            get
            {
                foreach (var isOnWhiteList in new[] { true, false })
                {
                    foreach (var isOnDomainList in new[] { true, false })
                    {
                        foreach (var isOnExceptionList in new[] { true, false })
                        {
                            foreach (var isOnTransformList in new[] { true, false })
                            {
                                yield return MemberDataHelper.AsData(isOnWhiteList, isOnDomainList, isOnExceptionList, isOnTransformList);
                            }
                        }
                    }
                }
            }
        }

        public static ILoginDiscontinuationConfiguration CreateConfiguration(bool isOnWhiteList, bool isOnDomainList, bool isOnExceptionList, bool isOnTransformList)
        {
            var emails = isOnWhiteList ? new[] { _email } : new[] { _incorrectEmail };
            var domains = isOnDomainList ? new[] { _domain } : new[] { _incorrectDomain };
            var exceptions = isOnExceptionList ? new[] { _email } : new[] { _incorrectException };
            var shouldTransforms = isOnTransformList ? new[] { _email } : new[] { _incorrectException };

            return new LoginDiscontinuationConfiguration(emails, domains, exceptions, shouldTransforms);
        }

        public class TheIsLoginDiscontinuedMethod
        {
            public static IEnumerable<object[]> IfPasswordLoginReturnsTrueIfOnWhitelists_Data
            {
                get
                {
                    foreach (var credentialPasswordType in new[] {
                        CredentialTypes.Password.Pbkdf2,
                        CredentialTypes.Password.Sha1,
                        CredentialTypes.Password.V3 })
                    {
                        foreach (var isOnWhiteList in new[] { true, false })
                        {
                            foreach (var isOnDomainList in new[] { true, false })
                            {
                                foreach (var isOnExceptionList in new[] { true, false })
                                {
                                    foreach (var isOnTransformList in new[] { true, false })
                                    {
                                        yield return MemberDataHelper.AsData(credentialPasswordType, isOnWhiteList, isOnDomainList, isOnExceptionList, isOnTransformList);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(IfPasswordLoginReturnsTrueIfOnWhitelists_Data))]
            public void IfPasswordLoginReturnsTrueIfOnWhitelists(string credentialPasswordType, bool isOnWhiteList, bool isOnDomainList, bool isOnExceptionList, bool isOnTransformList)
            {
                TestIsLoginDiscontinued(credentialPasswordType, isOnWhiteList, isOnDomainList, isOnExceptionList, isOnTransformList, 
                    expectedResult: (isOnWhiteList || isOnDomainList) && !isOnExceptionList);
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
                        foreach (var isOnWhiteList in new[] { true, false })
                        {
                            foreach (var isOnDomainList in new[] { true, false })
                            {
                                foreach (var isOnExceptionList in new[] { true, false })
                                {
                                    foreach (var isOnTransformList in new[] { true, false })
                                    {
                                        yield return MemberDataHelper.AsData(credentialType, isOnWhiteList, isOnDomainList, isOnExceptionList, isOnTransformList);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(IfNotPasswordLoginReturnsFalse_Data))]
            public void IfNotPasswordLoginReturnsFalse(string credentialType, bool isOnWhiteList, bool isOnDomainList, bool isOnExceptionList, bool isOnTransformList)
            {
                TestIsLoginDiscontinued(credentialType, isOnWhiteList, isOnDomainList, isOnExceptionList, isOnTransformList, expectedResult: false);
            }

            private void TestIsLoginDiscontinued(string credentialType, bool isOnWhiteList, bool isOnDomainList, bool isOnExceptionList, bool isOnTransformList, bool expectedResult)
            {
                // Arrange
                var credential = new Credential(credentialType, "value");
                var user = new User("test") { EmailAddress = _email, Credentials = new[] { credential } };
                var authUser = new AuthenticatedUser(user, credential);

                var config = CreateConfiguration(isOnWhiteList, isOnDomainList, isOnExceptionList, isOnTransformList);

                // Act
                var result = config.IsLoginDiscontinued(authUser);

                // Assert
                Assert.Equal(expectedResult, result);
            }
        }

        public class TheSupportedForUserMethods
        {
            public static IEnumerable<object[]> PossibleListStates => PossibleListStates;

            public void IsSupportedAsExpected(bool isOnWhiteList, bool isOnDomainList, bool isOnExceptionList, bool isOnTransformList)
            {
                // Arrange
                var user = new User("test") { EmailAddress = _email };

                var config = CreateConfiguration(isOnWhiteList, isOnDomainList, isOnExceptionList, isOnTransformList);

                // Act
                var areOrganizationsSupported = config.AreOrganizationsSupportedForUser(user);
                var shouldTransform = config.ShouldUserTransformIntoOrganization(user);

                // Assert
                Assert.Equal(isOnWhiteList || isOnDomainList, areOrganizationsSupported);
                Assert.Equal(isOnTransformList, shouldTransform);
            }

            public void IsUnsupportedWhenNull()
            {
                var config = new LoginDiscontinuationConfiguration();

                var areOrganizationsSupported = config.AreOrganizationsSupportedForUser(null);
                var shouldTransform = config.ShouldUserTransformIntoOrganization(null);

                Assert.False(areOrganizationsSupported);
                Assert.False(shouldTransform);
            }
        }
    }
}
