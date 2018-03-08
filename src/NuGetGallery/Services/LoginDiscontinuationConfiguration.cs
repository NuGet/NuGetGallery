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
        public HashSet<string> ForceTransformationForEmailAddresses { get; }

        public LoginDiscontinuationConfiguration()
            : this(Enumerable.Empty<string>(), 
                  Enumerable.Empty<string>(), 
                  Enumerable.Empty<string>(), 
                  Enumerable.Empty<string>())
        {
        }

        [JsonConstructor]
        public LoginDiscontinuationConfiguration(
            IEnumerable<string> discontinuedForEmailAddresses,
            IEnumerable<string> discontinuedForDomains,
            IEnumerable<string> exceptionsForEmailAddresses,
            IEnumerable<string> forceTransformationForEmailAddresses)
        {
            DiscontinuedForEmailAddresses = new HashSet<string>(discontinuedForEmailAddresses);
            DiscontinuedForDomains = new HashSet<string>(discontinuedForDomains);
            ExceptionsForEmailAddresses = new HashSet<string>(exceptionsForEmailAddresses);
            ForceTransformationForEmailAddresses = new HashSet<string>(forceTransformationForEmailAddresses);
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
                AreOrganizationsSupportedForUser(authUser.User) &&
                !ExceptionsForEmailAddresses.Contains(email.Address);
        }

        public bool AreOrganizationsSupportedForUser(User user)
        {
            if (user == null)
            {
                return false;
            }

            var email = user.ToMailAddress();
            return
                DiscontinuedForDomains.Contains(email.Host, StringComparer.OrdinalIgnoreCase) ||
                DiscontinuedForEmailAddresses.Contains(email.Address);
        }

        public bool ShouldUserTransformIntoOrganization(User user)
        {
            if (user == null)
            {
                return false;
            }

            var email = user.ToMailAddress();
            return ForceTransformationForEmailAddresses.Contains(email.Address);
        }
    }

    public interface ILoginDiscontinuationConfiguration
    {
        bool IsLoginDiscontinued(AuthenticatedUser authUser);
        bool AreOrganizationsSupportedForUser(User user);
        bool ShouldUserTransformIntoOrganization(User user);
    }
}