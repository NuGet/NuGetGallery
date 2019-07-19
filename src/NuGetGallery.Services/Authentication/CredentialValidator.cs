// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Helpers;
using NuGet.Services.Entities;
using NuGetGallery.Services.Authentication;

namespace NuGetGallery.Infrastructure.Authentication
{
    public class CredentialValidator : ICredentialValidator
    {
        public static readonly Dictionary<string, Func<string, Credential, bool>> Validators = new Dictionary<string, Func<string, Credential, bool>>(StringComparer.OrdinalIgnoreCase) {
            { CredentialTypes.Password.V3, (password, cred) => V3Hasher.VerifyHash(hashedData: cred.Value, providedInput: password) },
            { CredentialTypes.Password.Pbkdf2, (password, cred) => Crypto.VerifyHashedPassword(hashedPassword: cred.Value, password: password) },
            { CredentialTypes.Password.Sha1, (password, cred) => LegacyHasher.VerifyHash(cred.Value, password, ServicesConstants.Sha1HashAlgorithmId) }
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

        public IList<Credential> GetValidCredentialsForApiKey(IQueryable<Credential> allCredentials, string providedApiKey)
        {
            var results = new List<Credential>();

            if (ApiKeyV4.TryParse(providedApiKey, out ApiKeyV4 apiKeyV4))
            {
                var foundApiKeys = allCredentials.Where(c => c.Type == CredentialTypes.ApiKey.V4 &&
                                                             c.Value.StartsWith(apiKeyV4.IdPart)).ToList();

                // There shouldn't be duplications in the id part because it's long enough, but we shouldn't assume that.
                results = foundApiKeys.Where(c => apiKeyV4.Verify(c.Value)).ToList();
            }
            else
            {
                // Try to authenticate as APIKey V1/V2/V3/Verify
                if (ApiKeyV3.TryParse(providedApiKey, out var v3ApiKey))
                {
                    results = allCredentials.Where(c => c.Type.StartsWith(CredentialTypes.ApiKey.Prefix) &&
                                                        (c.Value == providedApiKey || c.Value.StartsWith(v3ApiKey.IdPart))).ToList();

                    results = results.Where(credential =>
                    {
                        switch (credential.Type)
                        {
                            case CredentialTypes.ApiKey.V1:
                            case CredentialTypes.ApiKey.V2:
                            case CredentialTypes.ApiKey.VerifyV1:
                                {
                                    return credential.Value == providedApiKey;
                                }
                            case CredentialTypes.ApiKey.V3:
                                {
                                    return v3ApiKey.Verify(credential.Value);
                                }

                            default:
                                {
                                    return false;
                                }
                        }
                    }).ToList();
                }
            }

            return results;
        }
    }
}