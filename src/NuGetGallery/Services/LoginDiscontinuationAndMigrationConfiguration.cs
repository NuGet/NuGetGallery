// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public class LoginDiscontinuationAndMigrationConfiguration : ILoginDiscontinuationAndMigrationConfiguration
    {
        public IEnumerable<string> DiscontinuedForDomains { get; }
        public IEnumerable<string> ExceptionsForEmailAddresses { get; }

        [JsonConstructor]
        public LoginDiscontinuationAndMigrationConfiguration(
            IEnumerable<string> discontinuedForDomains,
            IEnumerable<string> exceptionsForEmailAddresses)
        {
            DiscontinuedForDomains = discontinuedForDomains;
            ExceptionsForEmailAddresses = exceptionsForEmailAddresses;
        }

        public bool IsLoginDiscontinued(AuthenticatedUser authUser)
        {
            var email = authUser.User.ToMailAddress();
            return
                authUser.CredentialUsed.IsPassword() &&
                AreOrganizationsSupportedForUser(authUser.User) &&
                !ExceptionsForEmailAddresses.Contains(email.Address);
        }

        public bool AreOrganizationsSupportedForUser(User user)
        {
            var email = user.ToMailAddress();
            return DiscontinuedForDomains.Contains(email.Host, StringComparer.OrdinalIgnoreCase);
        }
    }

    public interface ILoginDiscontinuationAndMigrationConfiguration
    {
        bool IsLoginDiscontinued(AuthenticatedUser authUser);
        bool AreOrganizationsSupportedForUser(User user);
    }
}