// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationEntryKey
    {
        public RegistrationEntryKey(RegistrationKey registrationKey, string version)
        {
            RegistrationKey = registrationKey;
            Version = version;
        }

        public RegistrationKey RegistrationKey { get; set; }
        public string Version { get; set; }
            
        public override string ToString()
        {
            return RegistrationKey.ToString() + "/" + Version;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            RegistrationEntryKey rhs = obj as RegistrationEntryKey;

            if (rhs == null)
            {
                return false;
            }

            return (RegistrationKey.Equals(rhs.RegistrationKey)) && (Version == rhs.Version); 
        }
    }
}
