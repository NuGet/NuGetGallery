// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication.Providers
{
    public class AuthInformation
    {
        public string Identity { get; private set; }

        public string Name { get; private set; }

        public string Email { get; private set; }

        public string TenantId { get; private set; }

        public string AuthenticationType { get; private set; }

        public AuthInformation() { }

        public AuthInformation(string identity, string name, string email, string tenantId = null)
        {
            Identity = identity;
            Name = name;
            Email = email;
            TenantId = tenantId;
        }
    }
}
