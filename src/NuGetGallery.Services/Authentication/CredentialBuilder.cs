// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Services.Authentication;

namespace NuGetGallery.Infrastructure.Authentication
{
    public class CredentialBuilder : ICredentialBuilder
    {
        public const string LatestPasswordType = CredentialTypes.Password.V3;

        private static readonly IReadOnlyDictionary<string, IReadOnlyList<IActionRequiringEntityPermissions>> AllowedActionToActionRequiringEntityPermissionsMap = new Dictionary<string, IReadOnlyList<IActionRequiringEntityPermissions>>
        {
            { NuGetScopes.PackagePush, new IActionRequiringEntityPermissions[] { ActionsRequiringPermissions.UploadNewPackageId, ActionsRequiringPermissions.UploadNewPackageVersion } },
            { NuGetScopes.PackagePushVersion, new [] { ActionsRequiringPermissions.UploadNewPackageVersion } },
            { NuGetScopes.PackageUnlist, new [] { ActionsRequiringPermissions.UnlistOrRelistPackage } },
            { NuGetScopes.PackageVerify, new [] { ActionsRequiringPermissions.VerifyPackage } },
        };

        public Credential CreatePasswordCredential(string plaintextPassword)
        {
            return new Credential(
                LatestPasswordType,
                V3Hasher.GenerateHash(plaintextPassword));
        }

        public Credential CreateShortLivedApiKey(TimeSpan expiration, FederatedCredentialPolicy policy, string galleryEnvironment, out string plaintextApiKey)
        {
            if (policy.PackageOwner is null)
            {
                throw new ArgumentException($"The {nameof(policy.PackageOwner)} property on the policy must not be null.");
            }

            if (expiration <= TimeSpan.Zero || expiration > TimeSpan.FromHours(1))
            {
                throw new ArgumentOutOfRangeException(nameof(expiration));
            }

            var allocationTime = DateTime.UtcNow;
            allocationTime = allocationTime.AddSeconds(-allocationTime.Second).AddMilliseconds(-allocationTime.Millisecond);

            var apiKey = ApiKeyV5.Create(allocationTime, ApiKeyV5.GetEnvironment(galleryEnvironment), policy.PackageOwnerUserKey, ApiKeyV5.KnownApiKeyTypes.ShortLived, expiration);
            plaintextApiKey = apiKey.PlaintextApiKey;

            var credential = new Credential(CredentialTypes.ApiKey.V5, apiKey.HashedApiKey, expiration: expiration);

            credential.FederatedCredentialPolicy = policy;
            credential.Description = "Short-lived API key generated via a federated credential";
            credential.Scopes = [new Scope(policy.PackageOwner, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.All)];

            return credential;
        }

        public Credential CreateApiKey(TimeSpan? expiration, out string plaintextApiKey)
        {
            var apiKey = ApiKeyV4.Create();

            plaintextApiKey = apiKey.PlaintextApiKey;

            return new Credential(
               CredentialTypes.ApiKey.V4,
               apiKey.HashedApiKey,
               expiration: expiration);
        }

        public Credential CreatePackageVerificationApiKey(Credential originalApiKey, string id)
        {
            var credential = new Credential(
               CredentialTypes.ApiKey.VerifyV1,
               CreateKeyString(),
               expiration: TimeSpan.FromDays(1));

            var ownerKeys = originalApiKey.Scopes
                .Select(s => s.OwnerKey)
                .Distinct().ToArray();

            if (ownerKeys.Length == 0)
            {
                // Legacy API key with no owner scope.
                credential.Scopes = new[] { new Scope(
                    owner: null,
                    subject: id,
                    allowedAction: NuGetScopes.PackageVerify)
                };
            }
            else
            {
                credential.Scopes = ownerKeys
                    .Select(key => new Scope(
                        ownerKey: key,
                        subject: id,
                        allowedAction: NuGetScopes.PackageVerify))
                    .ToArray();
            }

            return credential;
        }

        public Credential CreateExternalCredential(string issuer, string value, string identity, string tenantId = null)
        {
            return new Credential(CredentialTypes.External.Prefix + issuer, value)
            {
                Identity = identity,
                TenantId = tenantId
            };
        }

        public IList<Scope> BuildScopes(User scopeOwner, string[] scopes, string[] subjects)
        {
            var result = new List<Scope>();

            var subjectsList = subjects?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();

            // No package filtering information was provided. So allow any pattern.
            if (!subjectsList.Any())
            {
                subjectsList.Add(NuGetPackagePattern.AllInclusivePattern);
            }

            if (scopes != null)
            {
                foreach (var scope in scopes)
                {
                    result.AddRange(subjectsList.Select(subject => new Scope(scopeOwner, subject, scope)));
                }
            }
            else
            {
                result.AddRange(subjectsList.Select(subject => new Scope(scopeOwner, subject, NuGetScopes.All)));
            }

            return result;
        }

        public bool VerifyScopes(User currentUser, IEnumerable<Scope> scopes)
        {
            if (!scopes.Any())
            {
                // All API keys must have at least one scope.
                return false;
            }

            foreach (var scope in scopes)
            {
                if (string.IsNullOrEmpty(scope.AllowedAction))
                {
                    // All scopes must have an allowed action.
                    return false;
                }

                // Get the list of actions allowed by this scope.
                var actions = new List<IActionRequiringEntityPermissions>();
                foreach (var allowedAction in AllowedActionToActionRequiringEntityPermissionsMap.Keys)
                {
                    if (scope.AllowsActions(allowedAction))
                    {
                        actions.AddRange(AllowedActionToActionRequiringEntityPermissionsMap[allowedAction]);
                    }
                }

                if (!actions.Any())
                {
                    // A scope should allow at least one action.
                    return false;
                }

                foreach (var action in actions)
                {
                    if (!action.IsAllowedOnBehalfOfAccount(currentUser, scope.Owner))
                    {
                        // The user must be able to perform the actions allowed by the scope on behalf of the scope's owner.
                        return false;
                    }
                }
            }

            return true;
        }

        private static string CreateKeyString()
        {
            return Guid.NewGuid().ToString().ToLowerInvariant();
        }
    }
}
