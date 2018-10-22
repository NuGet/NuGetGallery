// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Services.Entities;

namespace NuGetGallery.Services
{
    public sealed class SymbolsConfiguration : ISymbolsConfiguration
    {
        public bool IsSymbolsUploadEnabledForAll { get; }
        public HashSet<string> AlwaysEnabledForEmailAddresses { get; }
        public HashSet<string> AlwaysEnabledForDomains { get; }

        public SymbolsConfiguration()
            : this(isSymbolsUploadEnabledForAll: false,
                alwaysEnabledForDomains: Enumerable.Empty<string>(),
                alwaysEnabledForEmailAddresses: Enumerable.Empty<string>())
        {
        }

        [JsonConstructor]
        public SymbolsConfiguration(
            bool isSymbolsUploadEnabledForAll,
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

            IsSymbolsUploadEnabledForAll = isSymbolsUploadEnabledForAll;
            AlwaysEnabledForDomains = new HashSet<string>(alwaysEnabledForDomains, StringComparer.OrdinalIgnoreCase);
            AlwaysEnabledForEmailAddresses = new HashSet<string>(alwaysEnabledForEmailAddresses, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsSymbolsUploadEnabledForUser(User user)
        {
            if (user == null)
            {
                return false;
            }

            var email = user.ToMailAddress();

            return IsSymbolsUploadEnabledForAll ||
                AlwaysEnabledForDomains.Contains(email.Host) ||
                AlwaysEnabledForEmailAddresses.Contains(email.Address);
        }
    }
}