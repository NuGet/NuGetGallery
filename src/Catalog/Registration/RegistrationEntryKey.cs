// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationEntryKey
    {
        private readonly string _normalizedVersion;

        public RegistrationEntryKey(RegistrationKey registrationKey, string version)
        {
            RegistrationKey = registrationKey;
            Version = version;
            _normalizedVersion = NuGetVersionUtility.NormalizeVersion(version).ToLowerInvariant();
        }

        public RegistrationKey RegistrationKey { get; }
        public string Version { get; }
            
        public override string ToString()
        {
            return RegistrationKey.ToString() + "/" + Version;
        }

        public override int GetHashCode()
        {
            return $"{RegistrationKey}/{_normalizedVersion}".GetHashCode();
        }

        public override bool Equals(object obj)
        {
            RegistrationEntryKey rhs = obj as RegistrationEntryKey;

            if (rhs == null)
            {
                return false;
            }

            return RegistrationKey.Equals(rhs.RegistrationKey) &&
                   _normalizedVersion == rhs._normalizedVersion; 
        }
    }
}
