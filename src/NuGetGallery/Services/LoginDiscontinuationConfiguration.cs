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
        internal HashSet<string> DiscontinuedForEmailAddresses { get; }
        internal HashSet<string> DiscontinuedForDomains { get; }
        internal HashSet<string> ExceptionsForEmailAddresses { get; }
        internal HashSet<string> ForceTransformationToOrganizationForEmailAddresses { get; }

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
            IEnumerable<string> forceTransformationToOrganizationForEmailAddresses)
        {
            DiscontinuedForEmailAddresses = new HashSet<string>(discontinuedForEmailAddresses);
            DiscontinuedForDomains = new HashSet<string>(discontinuedForDomains);
            ExceptionsForEmailAddresses = new HashSet<string>(exceptionsForEmailAddresses);
            ForceTransformationToOrganizationForEmailAddresses = new HashSet<string>(forceTransformationToOrganizationForEmailAddresses);
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
                IsUserOnWhitelist(authUser.User) &&
                !ExceptionsForEmailAddresses.Contains(email.Address);
        }

        public bool IsUserOnWhitelist(User user)
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
            return ForceTransformationToOrganizationForEmailAddresses.Contains(email.Address);
        }
    }

    public interface ILoginDiscontinuationConfiguration
    {
        bool IsLoginDiscontinued(AuthenticatedUser authUser);
        bool IsUserOnWhitelist(User user);
        bool ShouldUserTransformIntoOrganization(User user);
    }
}