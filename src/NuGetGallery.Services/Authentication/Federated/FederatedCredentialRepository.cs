// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using System.Threading.Tasks;
using System.Linq;
using System.Data;
using System.Collections.Generic;
using System.Data.Entity;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public interface IFederatedCredentialRepository
    {
        Task AddPolicyAsync(FederatedCredentialPolicy policy, bool saveChanges);
        Task SaveFederatedCredentialAsync(FederatedCredential federatedCredential, bool saveChanges);
        IReadOnlyList<FederatedCredentialPolicy> GetPoliciesCreatedByUser(int userKey);
        FederatedCredentialPolicy? GetPolicyByKey(int policyKey);
        Task DeletePolicyAsync(FederatedCredentialPolicy policy, bool saveChanges);
    }

    public class FederatedCredentialRepository : IFederatedCredentialRepository
    {
        private readonly IEntityRepository<FederatedCredentialPolicy> _policyRepository;
        private readonly IEntityRepository<FederatedCredential> _federatedCredentialRepository;

        public FederatedCredentialRepository(
            IEntityRepository<FederatedCredentialPolicy> policyRepository,
            IEntityRepository<FederatedCredential> federatedCredentialRepository)
        {
            _policyRepository = policyRepository;
            _federatedCredentialRepository = federatedCredentialRepository;
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

        public async Task SaveFederatedCredentialAsync(FederatedCredential federatedCredential, bool saveChanges)
        {
            _federatedCredentialRepository.InsertOnCommit(federatedCredential);

            if (saveChanges)
            {
                await _federatedCredentialRepository.CommitChangesAsync();
            }
        }

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
