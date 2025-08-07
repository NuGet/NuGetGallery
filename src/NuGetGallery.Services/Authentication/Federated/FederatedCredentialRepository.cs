// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public interface IFederatedCredentialRepository
    {
        Task AddPolicyAsync(FederatedCredentialPolicy policy, bool saveChanges);
        Task SaveFederatedCredentialAsync(FederatedCredential federatedCredential, bool saveChanges);
        Task SavePoliciesAsync();
        IReadOnlyList<FederatedCredentialPolicy> GetPoliciesCreatedByUser(int userKey);
        FederatedCredentialPolicy? GetPolicyByKey(int policyKey);
        IReadOnlyList<Credential> GetShortLivedApiKeysForPolicy(int policyKey);
        IReadOnlyList<FederatedCredentialPolicy> GetPoliciesRelatedToUserKeys(IReadOnlyList<int> userKeys);
        Task DeletePolicyAsync(FederatedCredentialPolicy policy, bool saveChanges);
    }

    public class FederatedCredentialRepository : IFederatedCredentialRepository
    {
        private readonly IEntityRepository<FederatedCredentialPolicy> _policyRepository;
        private readonly IEntityRepository<FederatedCredential> _federatedCredentialRepository;
        private readonly IEntityRepository<Credential> _credentialRepository;

        public FederatedCredentialRepository(
            IEntityRepository<FederatedCredentialPolicy> policyRepository,
            IEntityRepository<FederatedCredential> federatedCredentialRepository,
            IEntityRepository<Credential> credentialRepository)
        {
            _policyRepository = policyRepository;
            _federatedCredentialRepository = federatedCredentialRepository;
            _credentialRepository = credentialRepository;
        }

        public IReadOnlyList<FederatedCredentialPolicy> GetPoliciesCreatedByUser(int userKey)
        {
            return _policyRepository
                .GetAll()
                .Where(p => p.CreatedByUserKey == userKey)
                .ToList();
        }

        public FederatedCredentialPolicy? GetPolicyByKey(int policyKey)
        {
            return _policyRepository
                .GetAll()
                .Where(p => p.Key == policyKey)
                .Include(p => p.CreatedBy)
                .FirstOrDefault();
        }

        public IReadOnlyList<Credential> GetShortLivedApiKeysForPolicy(int policyKey)
        {
            return _credentialRepository
                .GetAll()
                .Where(c => c.FederatedCredentialPolicyKey == policyKey)
                .Where(c => c.Type == CredentialTypes.ApiKey.V4 || c.Type == CredentialTypes.ApiKey.V5)
                .ToList();
        }

        public IReadOnlyList<FederatedCredentialPolicy> GetPoliciesRelatedToUserKeys(IReadOnlyList<int> userKeys)
        {
            return _policyRepository
                .GetAll()
                .Where(x => userKeys.Contains(x.CreatedByUserKey) || userKeys.Contains(x.PackageOwnerUserKey))
                .Include(x => x.CreatedBy)
                .Include(x => x.PackageOwner)
                .ToList();
        }

        public async Task SaveFederatedCredentialAsync(FederatedCredential federatedCredential, bool saveChanges)
        {
            _federatedCredentialRepository.InsertOnCommit(federatedCredential);

            if (saveChanges)
            {
                await _federatedCredentialRepository.CommitChangesAsync();
            }
        }

        public Task SavePoliciesAsync() => _policyRepository.CommitChangesAsync();

        public async Task AddPolicyAsync(FederatedCredentialPolicy policy, bool saveChanges)
        {
            _policyRepository.InsertOnCommit(policy);

            if (saveChanges)
            {
                await _policyRepository.CommitChangesAsync();
            }
        }

        public async Task DeletePolicyAsync(FederatedCredentialPolicy policy, bool saveChanges)
        {
            _policyRepository.DeleteOnCommit(policy);

            if (saveChanges)
            {
                await _policyRepository.CommitChangesAsync();
            }
        }
    }
}
