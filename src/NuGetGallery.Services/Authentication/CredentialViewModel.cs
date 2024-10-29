// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGetGallery.Authentication;
using NuGetGallery.Authentication.Providers;

namespace NuGetGallery
{
    public class CredentialViewModel
    {
        public int Key { get; set; }
        public string Type { get; set; }
        public string TypeCaption { get; set; }
        public string Identity { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Expires { get; set; }
        public CredentialKind Kind { get; set; }
        public AuthenticatorUI AuthUI { get; set; }
        public string Description { get; set; }
        public List<ScopeViewModel> Scopes { get; set; }
        public bool HasExpired { get; set; }
        public string Value { get; set; }
        public TimeSpan? ExpirationDuration { get; set; }
        public string RevocationSource { get; set; }

        public bool IsNonScopedApiKey
        {
            get
            {
                return CredentialTypes.IsApiKey(Type) && !Scopes.AnySafe();
            }
        }

        public CredentialTypeInfo GetCredentialTypeInfo()
        {
            if (CredentialTypes.IsApiKey(Type))
            {
                return new CredentialTypeInfo(Type, true, Description);
            }
            else
            {
                return AuthUI == null
                    ? new CredentialTypeInfo(Type, false, TypeCaption)
                    : new CredentialTypeInfo(Type, false, AuthUI.AccountNoun);
            }
        }
    }
}
