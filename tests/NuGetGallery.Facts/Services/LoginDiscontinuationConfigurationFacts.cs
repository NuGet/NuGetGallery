// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using System.Collections.Generic;
using System.Linq;
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
        private const string _tenant = "tenantId";
        private const string _incorrectTenant = "wrongTenantId";

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
                                foreach (var isOnTenantPairList in new[] { true, false })
                                {
                                    foreach (var isWrongCase in new[] { true, false })
                                    {
                                        yield return MemberDataHelper.AsData(isOnWhiteList, isOnDomainList, isOnExceptionList, isOnTransformList, isOnTenantPairList, isWrongCase);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static ILoginDiscontinuationConfiguration CreateConfiguration(bool isOnWhiteList, bool isOnDomainList, bool isOnExceptionList, bool isOnTransformList, bool isOnTenantPairList, bool isWrongCase)
        {
            var emails = isOnWhiteList ? new[] { ToUppercaseIfWrongCase(_email, isWrongCase) } : new[] { ToUppercaseIfWrongCase(_incorrectEmail, isWrongCase) };
            var domains = isOnDomainList ? new[] { ToUppercaseIfWrongCase(_domain, isWrongCase) } : new[] { ToUppercaseIfWrongCase(_incorrectDomain, isWrongCase) };
            var exceptions = isOnExceptionList ? new[] { ToUppercaseIfWrongCase(_email, isWrongCase) } : new[] { ToUppercaseIfWrongCase(_incorrectException, isWrongCase) };
            var shouldTransforms = isOnTransformList ? new[] { ToUppercaseIfWrongCase(_email, isWrongCase) } : new[] { ToUppercaseIfWrongCase(_incorrectException, isWrongCase) };
            var orgTenantPairs = isOnTenantPairList ?
                new[] { new OrganizationTenantPair(ToUppercaseIfWrongCase(_domain, isWrongCase), ToUppercaseIfWrongCase(_tenant, isWrongCase)) } :
                new[] { new OrganizationTenantPair(ToUppercaseIfWrongCase(_incorrectDomain, isWrongCase), ToUppercaseIfWrongCase(_incorrectTenant, isWrongCase)) };

            return new LoginDiscontinuationConfiguration(emails, domains, exceptions, shouldTransforms, orgTenantPairs, isPasswordDiscontinuedForAll: false);
        }

        private static string ToUppercaseIfWrongCase(string input, bool isWrongCase)
        {
            return isWrongCase ? input.ToUpperInvariant() : input;
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
                                        foreach (var isWrongCase in new[] { true, false })
                                        {
                                            yield return MemberDataHelper.AsData(credentialPasswordType, isOnWhiteList, isOnDomainList, isOnExceptionList, isOnTransformList, isWrongCase);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(IfPasswordLoginReturnsTrueIfOnWhitelists_Data))]
            public void IfPasswordLoginReturnsTrueIfOnWhitelists(string credentialPasswordType, bool isOnWhiteList, bool isOnDomainList, bool isOnExceptionList, bool isOnTransformList, bool isWrongCase)
            {
                TestIsLoginDiscontinued(credentialPasswordType, isOnWhiteList, isOnDomainList, isOnExceptionList, isOnTransformList, isWrongCase,
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
                                        foreach (var isWrongCase in new[] { true, false })
                                        {
                                            yield return MemberDataHelper.AsData(credentialType, isOnWhiteList, isOnDomainList, isOnExceptionList, isOnTransformList, isWrongCase);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(IfNotPasswordLoginReturnsFalse_Data))]
            public void IfNotPasswordLoginReturnsFalse(string credentialType, bool isOnWhiteList, bool isOnDomainList, bool isOnExceptionList, bool isOnTransformList, bool isWrongCase)
            {
                TestIsLoginDiscontinued(credentialType, isOnWhiteList, isOnDomainList, isOnExceptionList, isOnTransformList, isWrongCase, expectedResult: false);
            }

            private void TestIsLoginDiscontinued(string credentialType, bool isOnWhiteList, bool isOnDomainList, bool isOnExceptionList, bool isOnTransformList, bool isWrongCase, bool expectedResult)
            {
                // Arrange
                var credential = new Credential(credentialType, "value");
                var user = new User("test") { EmailAddress = _email, Credentials = new[] { credential } };
                var authUser = new AuthenticatedUser(user, credential);

                var config = CreateConfiguration(isOnWhiteList, isOnDomainList, isOnExceptionList, isOnTransformList, isOnTenantPairList: false, isWrongCase: isWrongCase);

                // Act
                var result = config.IsLoginDiscontinued(authUser);

                // Assert
                Assert.Equal(expectedResult, result);
            }
        }

        public class WhitelistMethodData
        {
            public bool IsOnWhiteList { get; }
            public bool IsOnDomainList { get; }
            public bool IsOnExceptionList { get; }
            public bool IsOnTransformList { get; }
            public bool IsOnTenantPairList { get; }
            public bool IsWrongCase { get; }
            
            public WhitelistMethodData(object[] data)
            {
                var boolData = data.Cast<bool>().ToArray();

                var isOnWhiteList = boolData[0];
                IsOnWhiteList = isOnWhiteList;

                var isOnDomainList = boolData[1];
                IsOnDomainList = isOnDomainList;

                var isOnExceptionList = boolData[2];
                IsOnExceptionList = isOnExceptionList;

                var isOnTransformList = boolData[3];
                IsOnTransformList = isOnTransformList;

                var isOnTenantPairList = boolData[4];
                IsOnTenantPairList = isOnTenantPairList;

                var isWrongCase = boolData[5];
                IsWrongCase = isWrongCase;
            }
        }

        public static IEnumerable<object[]> WhitelistBaseMethodReturnsExpected_Data =>
            PossibleListStates.Select(data =>
                MemberDataHelper.AsData(
                    new WhitelistMethodData(data)));

        public abstract class WhitelistBaseMethod
        {
            protected virtual User GetUser(WhitelistMethodData data)
            {
                return new User("test") { EmailAddress = _email };
            }

            protected abstract bool GetWhitelistValue(ILoginDiscontinuationConfiguration config, User user);

            public abstract bool GetExpectedValueForNonNull(WhitelistMethodData data);

            [Theory]
            [MemberData("ReturnsAsExpectedWhenNonNull_Data")]
            public void ReturnsExpectedWhenNonNull(WhitelistMethodData data)
            {
                // Arrange
                var user = GetUser(data);
                var config = GetConfiguration(data);

                // Act
                var whitelistValue = GetWhitelistValue(config, user);

                // Assert
                Assert.Equal(GetExpectedValueForNonNull(data), whitelistValue);
            }

            public abstract bool GetExpectedValueForNull(WhitelistMethodData data);

            [Theory]
            [MemberData("ReturnsFalseWhenNull_Data")]
            public void ReturnsExpectedWhenNull(WhitelistMethodData data)
            {
                // Arrange
                var config = GetConfiguration(data);

                // Act
                var whitelistValue = GetWhitelistValue(config, null);

                // Assert
                Assert.Equal(GetExpectedValueForNull(data), whitelistValue);
            }

            private ILoginDiscontinuationConfiguration GetConfiguration(WhitelistMethodData data)
            {
                return CreateConfiguration(data.IsOnWhiteList, data.IsOnDomainList, data.IsOnExceptionList, data.IsOnTransformList, data.IsOnTenantPairList, data.IsWrongCase);
            }
        }

        public class TheIsUserOnWhitelistMethod : WhitelistBaseMethod
        {
            public static IEnumerable<object[]> ReturnsAsExpectedWhenNonNull_Data => WhitelistBaseMethodReturnsExpected_Data;

            public override bool GetExpectedValueForNonNull(WhitelistMethodData data)
            {
                return data.IsOnWhiteList || data.IsOnDomainList;
            }

            public static IEnumerable<object[]> ReturnsFalseWhenNull_Data => WhitelistBaseMethodReturnsExpected_Data;

            public override bool GetExpectedValueForNull(WhitelistMethodData data)
            {
                return false;
            }

            protected override bool GetWhitelistValue(ILoginDiscontinuationConfiguration config, User user)
            {
                return config.IsUserOnWhitelist(user);
            }
        }

        public class TheShouldUserTransformIntoOrganizationMethod : WhitelistBaseMethod
        {
            public static IEnumerable<object[]> ReturnsAsExpectedWhenNonNull_Data => WhitelistBaseMethodReturnsExpected_Data;

            public override bool GetExpectedValueForNonNull(WhitelistMethodData data)
            {
                return data.IsOnTransformList;
            }

            public static IEnumerable<object[]> ReturnsFalseWhenNull_Data => WhitelistBaseMethodReturnsExpected_Data;

            public override bool GetExpectedValueForNull(WhitelistMethodData data)
            {
                return false;
            }

            protected override bool GetWhitelistValue(ILoginDiscontinuationConfiguration config, User user)
            {
                return config.ShouldUserTransformIntoOrganization(user);
            }
        }
    }
}
