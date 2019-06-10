// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery.Authentication
{
    public class LoginUserDetails
    {
        public AuthenticatedUser AuthenticatedUser { get; set; }

        public bool UsedMultiFactorAuthentication { get; set; }
    }

    public class AuthenticatedUser
    {
        public User User { get; private set; }
        public Credential CredentialUsed { get; private set; }

        public AuthenticatedUser(User user, Credential cred)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (cred == null)
            {
                throw new ArgumentNullException(nameof(cred));
            }

            User = user;
            CredentialUsed = cred;
        }
    }
}
