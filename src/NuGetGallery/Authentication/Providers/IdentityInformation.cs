// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication.Providers
{
    public class IdentityInformation
    {
        public string Identifier { get; private set; }

        public string Name { get; private set; }

        public string Email { get; private set; }

        public string TenantId { get; private set; }

        public string AuthenticationType { get; private set; }

        public IdentityInformation(string identifier, string name, string email, string authenticationType, string tenantId = null)
        {
            Identifier = identifier;
            Name = name;
            Email = email;
            AuthenticationType = authenticationType;
            TenantId = tenantId;
        }
    }
}
