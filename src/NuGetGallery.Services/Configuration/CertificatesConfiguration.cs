// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Services.Entities;

namespace NuGetGallery.Services
{
    public sealed class CertificatesConfiguration : ICertificatesConfiguration
    {
        public bool IsUIEnabledByDefault { get; }
        public HashSet<string> AlwaysEnabledForEmailAddresses { get; }
        public HashSet<string> AlwaysEnabledForDomains { get; }

        public CertificatesConfiguration()
            : this(isUIEnabledByDefault: false,
                alwaysEnabledForDomains: Enumerable.Empty<string>(),
                alwaysEnabledForEmailAddresses: Enumerable.Empty<string>())
        {
        }

        [JsonConstructor]
        public CertificatesConfiguration(
            bool isUIEnabledByDefault,
            IEnumerable<string> alwaysEnabledForDomains,
            IEnumerable<string> alwaysEnabledForEmailAddresses)
        {
            if (alwaysEnabledForDomains == null)
            {
                throw new ArgumentNullException(nameof(alwaysEnabledForDomains));
            }

            if (alwaysEnabledForEmailAddresses == null)
            {
                throw new ArgumentNullException(nameof(alwaysEnabledForEmailAddresses));
            }

            IsUIEnabledByDefault = isUIEnabledByDefault;
            AlwaysEnabledForDomains = new HashSet<string>(alwaysEnabledForDomains, StringComparer.OrdinalIgnoreCase);
            AlwaysEnabledForEmailAddresses = new HashSet<string>(alwaysEnabledForEmailAddresses, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsUIEnabledForUser(User user)
        {
            if (user == null)
            {
                return false;
            }

            var email = user.ToMailAddress();

            return IsUIEnabledByDefault ||
                AlwaysEnabledForDomains.Contains(email.Host) ||
                AlwaysEnabledForEmailAddresses.Contains(email.Address);
        }
    }
}