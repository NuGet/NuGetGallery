// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class CredentialTypesFacts
    {
        [Theory]
        [InlineData(CredentialTypes.Password.Sha1, true)]
        [InlineData(CredentialTypes.Password.Pbkdf2, true)]
        [InlineData(CredentialTypes.Password.V3, true)]
        [InlineData(CredentialTypes.ApiKey.V1, true)]
        [InlineData(CredentialTypes.ApiKey.V2, true)]
        [InlineData(CredentialTypes.ApiKey.V3, true)]
        [InlineData(CredentialTypes.ApiKey.V4, true)]
        [InlineData(CredentialTypes.External.MicrosoftAccount, true)]
        [InlineData(CredentialTypes.External.AzureActiveDirectoryAccount, true)]
        [InlineData(CredentialTypes.ApiKey.V5, false)]
        [InlineData(CredentialTypes.ApiKey.VerifyV1, false)]
        public void IsViewSupportedCredential(string credentialType, bool isViewSupported)
        {
            var credential = new Credential(credentialType, "testcredential");
            Assert.Equal(isViewSupported, credential.IsViewSupportedCredential());
        }

        [Theory]
        [InlineData(CredentialTypes.Password.Sha1)]
        [InlineData(CredentialTypes.Password.Pbkdf2)]
        [InlineData(CredentialTypes.Password.V3)]
        [InlineData(CredentialTypes.ApiKey.V1)]
        [InlineData(CredentialTypes.ApiKey.V2)]
        [InlineData(CredentialTypes.ApiKey.V3)]
        [InlineData(CredentialTypes.ApiKey.V4)]
        [InlineData(CredentialTypes.ApiKey.V5)]
        [InlineData(CredentialTypes.ApiKey.VerifyV1)]
        [InlineData(CredentialTypes.External.MicrosoftAccount)]
        [InlineData(CredentialTypes.External.AzureActiveDirectoryAccount)]
        public void IsSupportedCredential(string credentialType)
        {
            var credential = new Credential(credentialType, "testcredential");
            Assert.True(credential.IsSupportedCredential());
        }
    }
}
