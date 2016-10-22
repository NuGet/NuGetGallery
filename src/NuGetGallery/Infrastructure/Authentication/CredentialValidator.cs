// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web.Helpers;
using NuGetGallery.Services.Authentication;

namespace NuGetGallery.Infrastructure.Authentication
{
    public class CredentialValidator : ICredentialValidator
    {
        public static readonly Dictionary<string, Func<string, Credential, bool>> Validators = new Dictionary<string, Func<string, Credential, bool>>(StringComparer.OrdinalIgnoreCase) {
            { CredentialTypes.Password.V3, (password, cred) => V3Hasher.VerifyHash(hashedData: cred.Value, providedInput: password) },
            { CredentialTypes.Password.Pbkdf2, (password, cred) => Crypto.VerifyHashedPassword(hashedPassword: cred.Value, password: password) },
            { CredentialTypes.Password.Sha1, (password, cred) => LegacyHasher.VerifyHash(cred.Value, password, Constants.Sha1HashAlgorithmId) }
        };

        public bool ValidatePasswordCredential(Credential credential, string providedPassword)
        {
            Func<string, Credential, bool> validator;

            if (!Validators.TryGetValue(credential.Type, out validator))
            {
                return false;
            }

            return validator(providedPassword, credential);
        }
    }
}