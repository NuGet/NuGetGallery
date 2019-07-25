// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;

namespace NuGet.Services.KeyVault
{
    public class KeyVaultSecret : ISecret
    {
        public KeyVaultSecret(string name, string value, DateTimeOffset? expiryDate)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            Name = name;
            Value = value;
            Expiration = expiryDate;
        }

        public string Name { get; }

        public string Value { get; }
        public DateTimeOffset? Expiration { get; }

    }
}
