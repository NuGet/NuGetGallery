// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Identity.Client;

namespace NuGet.Services.Sql
{
    internal class AuthenticationResultWrapper : IAuthenticationResult
    {
        public AuthenticationResult Instance { get; }

        public AuthenticationResultWrapper(AuthenticationResult instance)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        public string AccessToken
        {
            get
            {
                return Instance.AccessToken;
            }
        }

        public DateTimeOffset ExpiresOn
        {
            get
            {
                return Instance.ExpiresOn;
            }
        }
    }
}