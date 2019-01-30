// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using System;
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
        private static User _user = new User("test") { EmailAddress = _email };

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
                                        foreach (var isPasswordDiscontinuedForAll in new[] { true, false })
                                        {
                                            yield return MemberDataHelper.AsData(
                                                isOnWhiteList, 
                                                isOnDomainList, 
                                                isOnExceptionList, 
                                                isOnTransformList, 
                                                isOnTenantPairList, 
                                                isWrongCase, 
                                                isPasswordDiscontinuedForAll);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static ILoginDiscontinuationConfiguration CreateConfiguration(WhitelistMethodData data)
        {
            return CreateConfiguration(
                data.IsOnWhiteList, 
                data.IsOnDomainList, 
                data.IsOnExceptionList, 
                data.IsOnTransformList, 
                data.IsOnTenantPairList, 
                data.IsWrongCase, 
                data.IsPasswordDiscontinuedForAll);
        }

        public static ILoginDiscontinuationConfiguration CreateConfiguration(
            bool isOnWhiteList, 
            bool isOnDomainList, 
            bool isOnExceptionList, 
            bool isOnTransformList, 
            bool isOnTenantPairList, 
            bool isWrongCase, 
            bool isPasswordDiscontinuedForAll)
        {
            var emails = ToUppercaseIfWrongCase(new[] { isOnWhiteList ? _email : _incorrectEmail }, isWrongCase);
            var domains = ToUppercaseIfWrongCase(new[] { isOnDomainList ? _domain : _incorrectDomain }, isWrongCase);
            var exceptions = ToUppercaseIfWrongCase(new[] { isOnExceptionList ? _email : _incorrectException }, isWrongCase);
            var shouldTransforms = ToUppercaseIfWrongCase(new[] { isOnTransformList ? _email : _incorrectException }, isWrongCase);
            var orgTenantPairs = ToUppercaseIfWrongCase(new[] { isOnTenantPairList 
                ? new OrganizationTenantPair(_domain, _tenant) 
                : new OrganizationTenantPair(_incorrectDomain, _incorrectTenant) }, isWrongCase);

            return new LoginDiscontinuationConfiguration(emails, domains, exceptions, shouldTransforms, orgTenantPairs, isPasswordDiscontinuedForAll);
        }

        private static string[] ToUppercaseIfWrongCase(string[] input, bool isWrongCase)
        {
            return input
                .Select(i => ToUppercaseIfWrongCase(i, isWrongCase))
                .ToArray();
        }

        private static OrganizationTenantPair[] ToUppercaseIfWrongCase(OrganizationTenantPair[] input, bool isWrongCase)
        {
            return input
                .Select(i => new OrganizationTenantPair(
                    ToUppercaseIfWrongCase(i.EmailDomain, isWrongCase),
                    ToUppercaseIfWrongCase(i.TenantId, isWrongCase)))
                .ToArray();
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
                                            foreach (var isPasswordDiscontinuedForAll in new[] { true, false })
                                            {
                                                yield return MemberDataHelper.AsData(
                                                    credentialPasswordType, 
                                                    isOnWhiteList, 
                                                    isOnDomainList, 
                                                    isOnExceptionList, 
                                                    isOnTransformList, 
                                                    isWrongCase, 
                                                    isPasswordDiscontinuedForAll);
                                            }
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
            public void IfPasswordLoginReturnsTrueIfOnWhitelists(
                string credentialPasswordType, 
                bool isOnWhiteList, 
                bool isOnDomainList, 
                bool isOnExceptionList, 
                bool isOnTransformList, 
                bool isWrongCase, 
                bool isPasswordDiscontinuedForAll)
            {
                TestIsLoginDiscontinued(
                    credentialPasswordType, 
                    isOnWhiteList, 
                    isOnDomainList, 
                    isOnExceptionList, 
                    isOnTransformList, 
                    isWrongCase, 
                    isPasswordDiscontinuedForAll,
                    expectedResult: (isPasswordDiscontinuedForAll || isOnWhiteList || isOnDomainList) && !isOnExceptionList);
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
                                            foreach (var isPasswordDiscontinuedForAll in new[] { true, false })
                                            {
                                                yield return MemberDataHelper.AsData(
                                                    credentialType, 
                                                    isOnWhiteList, 
                                                    isOnDomainList, 
                                                    isOnExceptionList, 
                                                    isOnTransformList, 
                                                    isWrongCase, 
                                                    isPasswordDiscontinuedForAll);
                                            }
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
            public void IfNotPasswordLoginReturnsFalse(
                string credentialType, 
                bool isOnWhiteList, 
                bool isOnDomainList, 
                bool isOnExceptionList, 
                bool isOnTransformList, 
                bool isWrongCase, 
                bool isPasswordDiscontinuedForAll)
            {
                TestIsLoginDiscontinued(
                    credentialType, 
                    isOnWhiteList, 
                    isOnDomainList, 
                    isOnExceptionList, 
                    isOnTransformList, 
                    isWrongCase, 
                    isPasswordDiscontinuedForAll, 
                    expectedResult: false);
            }

            private void TestIsLoginDiscontinued(
                string credentialType, 
                bool isOnWhiteList, 
                bool isOnDomainList, 
                bool isOnExceptionList, 
                bool isOnTransformList, 
                bool isWrongCase, 
                bool isPasswordDiscontinuedForAll, 
                bool expectedResult)
            {
                // Arrange
                var credential = new Credential(credentialType, "value");
                var user = new User("test") { EmailAddress = _email, Credentials = new[] { credential } };
                var authUser = new AuthenticatedUser(user, credential);

                var config = CreateConfiguration(
                    isOnWhiteList, 
                    isOnDomainList, 
                    isOnExceptionList, 
                    isOnTransformList, 
                    isOnTenantPairList: false, 
                    isWrongCase: isWrongCase, 
                    isPasswordDiscontinuedForAll: isPasswordDiscontinuedForAll);

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
            public bool IsPasswordDiscontinuedForAll { get; }

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

                var isPasswordDiscontinuedForAll = boolData[6];
                IsPasswordDiscontinuedForAll = isPasswordDiscontinuedForAll;
            }
        }

        public class TheWhiteListMethods
        {
            public static IEnumerable<object[]> AllPossibleWhiteListData =>
                PossibleListStates.Select(data =>
                    MemberDataHelper.AsData(
                        new WhitelistMethodData(data)));

            [Theory]
            [MemberData(nameof(AllPossibleWhiteListData))]
            public void ReturnsExpectedWhenNonNull(WhitelistMethodData data)
            {
                // Arrange
                var config = CreateConfiguration(data);

                // Act
                var isOnWhiteList = config.IsUserOnWhitelist(_user);
                var shouldTransformIntoOrganization = config.ShouldUserTransformIntoOrganization(_user);
                var isOnTenantPairList = config.IsTenantIdPolicySupportedForOrganization(_email, _tenant);

                // Assert
                Assert.Equal(data.IsOnWhiteList || data.IsOnDomainList, isOnWhiteList);
                Assert.Equal(data.IsOnTransformList, shouldTransformIntoOrganization);
                Assert.Equal(data.IsOnTenantPairList, isOnTenantPairList);
            }

            [Theory]
            [MemberData(nameof(AllPossibleWhiteListData))]
            public void ReturnsExpectedWhenNull(WhitelistMethodData data)
            {
                // Arrange
                var config = CreateConfiguration(data);

                // Act
                var isOnWhiteList = config.IsUserOnWhitelist(null);
                var shouldTransformIntoOrganization = config.ShouldUserTransformIntoOrganization(null);

                // Assert
                Assert.Equal(false, isOnWhiteList);
                Assert.Equal(false, shouldTransformIntoOrganization);
                
                foreach (var orgEmail in new[] { null, _email })
                {
                    foreach (var orgTenant in new[] { null, _tenant })
                    {
                        if (!string.IsNullOrEmpty(orgEmail) && !string.IsNullOrEmpty(orgTenant))
                        {
                            continue;
                        }

                        Assert.Throws<ArgumentException>(() => config.IsTenantIdPolicySupportedForOrganization(orgEmail, orgTenant));
                    }
                }
            }
        }
    }
}
