// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public class LoginDiscontinuationConfiguration : ILoginDiscontinuationConfiguration
    {
        public HashSet<string> DiscontinuedForEmailAddresses { get; }
        public HashSet<string> DiscontinuedForDomains { get; }
        public HashSet<string> ExceptionsForEmailAddresses { get; }

        [JsonConstructor]
        public LoginDiscontinuationConfiguration(
            IEnumerable<string> discontinuedForEmailAddresses,
            IEnumerable<string> discontinuedForDomains,
            IEnumerable<string> exceptionsForEmailAddresses)
        {
            DiscontinuedForEmailAddresses = new HashSet<string>(discontinuedForEmailAddresses);
            DiscontinuedForDomains = new HashSet<string>(discontinuedForDomains);
            ExceptionsForEmailAddresses = new HashSet<string>(exceptionsForEmailAddresses);
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
            return
                DiscontinuedForDomains.Contains(email.Host, StringComparer.OrdinalIgnoreCase) ||
                DiscontinuedForEmailAddresses.Contains(email.Address);
        }
    }

    public interface ILoginDiscontinuationConfiguration
    {
        bool IsLoginDiscontinued(AuthenticatedUser authUser);
        bool AreOrganizationsSupportedForUser(User user);
    }
}