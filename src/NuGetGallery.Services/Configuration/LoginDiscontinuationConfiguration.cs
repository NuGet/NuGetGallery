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
            : this(Array.Empty<string>(),
                  Array.Empty<string>(),
                  Array.Empty<string>(),
                  Array.Empty<string>(),
                  Array.Empty<OrganizationTenantPair>(),
                  isPasswordDiscontinuedForAll: false)
        {
        }

        [JsonConstructor]
        public LoginDiscontinuationConfiguration(
            ICollection<string> discontinuedForEmailAddresses,
            ICollection<string> discontinuedForDomains,
            ICollection<string> exceptionsForEmailAddresses,
            ICollection<string> forceTransformationToOrganizationForEmailAddresses,
            ICollection<OrganizationTenantPair> enabledOrganizationAadTenants,
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
                (IsPasswordDiscontinuedForAll || IsUserInAllowList(authUser.User)) &&
                !ExceptionsForEmailAddresses.Contains(email.Address);
        }

    }
}