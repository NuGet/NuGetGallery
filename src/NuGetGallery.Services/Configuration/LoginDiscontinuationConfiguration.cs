// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Login;

namespace NuGetGallery.Configuration
{
    public class LoginDiscontinuationConfiguration : LoginDiscontinuation, ILoginDiscontinuationConfiguration
    {
        public LoginDiscontinuationConfiguration()
            : this(Enumerable.Empty<string>(),
                  Enumerable.Empty<string>(),
                  Enumerable.Empty<string>(),
                  Enumerable.Empty<string>(),
                  Enumerable.Empty<OrganizationTenantPair>(),
                  isPasswordDiscontinuedForAll: false)
        {
        }

        [JsonConstructor]
        public LoginDiscontinuationConfiguration(
            IEnumerable<string> discontinuedForEmailAddresses,
            IEnumerable<string> discontinuedForDomains,
            IEnumerable<string> exceptionsForEmailAddresses,
            IEnumerable<string> forceTransformationToOrganizationForEmailAddresses,
            IEnumerable<OrganizationTenantPair> enabledOrganizationAadTenants,
            bool isPasswordDiscontinuedForAll) : base(
                discontinuedForEmailAddresses,
                discontinuedForDomains,
                exceptionsForEmailAddresses,
                forceTransformationToOrganizationForEmailAddresses,
                enabledOrganizationAadTenants,
                isPasswordDiscontinuedForAll)
        {
        }

        public bool IsLoginDiscontinued(AuthenticatedUser authUser)
        {
            if (authUser == null || authUser.User == null)
            {
                return false;
            }

            var email = authUser.User.ToMailAddress();
            return
                authUser.CredentialUsed.IsPassword() &&
                (IsPasswordDiscontinuedForAll || IsUserOnWhitelist(authUser.User)) &&
                !ExceptionsForEmailAddresses.Contains(email.Address);
        }

    }
}