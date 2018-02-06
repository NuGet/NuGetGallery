// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using Newtonsoft.Json;
using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using Xunit;

namespace NuGetGallery.Services
{
    public class LoginDeprecationServiceFacts
    {
        public class TheIsLoginDiscontinuedAsyncMethod : TestContainer
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
            public Task IfPasswordLoginAndUserIsOnListReturnsTrue(string credentialPasswordType)
            {
                return Test(credentialPasswordType, isOnDomainList: true, isOnExceptionList: false, expectedResult: true);
            }

            [Theory]
            [MemberData(nameof(CredentialPasswordTypes))]
            public Task IfPasswordLoginAndUserIsNotOnListReturnsFalse(string credentialPasswordType)
            {
                return Test(credentialPasswordType, isOnDomainList: false, isOnExceptionList: false, expectedResult: false);
            }

            [Theory]
            [MemberData(nameof(CredentialPasswordTypes))]
            public Task IfPasswordLoginAndUserIsOnListWithExceptionReturnsFalse(string credentialPasswordType)
            {
                return Test(credentialPasswordType, isOnDomainList: true, isOnExceptionList: true, expectedResult: false);
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
            public Task IfNotPasswordLoginReturnsFalse(string credentialType, bool isOnDomainList, bool isOnExceptionList)
            {
                return Test(credentialType, isOnDomainList, isOnExceptionList, expectedResult: false);
            }

            private async Task Test(string credentialType, bool isOnDomainList, bool isOnExceptionList, bool expectedResult)
            {
                // Arrange
                var credential = new Credential(credentialType, "value");
                var user = new User("test") { EmailAddress = _email, Credentials = new[] { credential } };
                var authUser = new AuthenticatedUser(user, credential);

                var domains = isOnDomainList ? new[] { _domain } : new[] { _incorrectDomain };
                var exceptions = isOnExceptionList ? new[] { _email } : new[] { _incorrectEmail };

                var config = new LoginDeprecationService.PasswordLoginDiscontinuationConfiguration(domains, exceptions);
                var configString = JsonConvert.SerializeObject(config);

                GetMock<IContentService>()
                    .Setup(x => x.GetContentItemAsync(Constants.ContentNames.PasswordLoginDiscontinuationConfiguration, It.IsAny<TimeSpan>()))
                    .Returns(Task.FromResult<IHtmlString>(new HtmlString(configString)));

                // Act
                var result = await Get<LoginDeprecationService>().IsLoginDiscontinuedAsync(authUser);

                // Assert
                Assert.Equal(expectedResult, result);
            }
        }
    }
}
