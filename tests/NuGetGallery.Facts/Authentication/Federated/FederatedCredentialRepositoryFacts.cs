// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using Xunit;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public class FederatedCredentialRepositoryFacts
    {
        public class TheGetPoliciesCreatedByUserMethod : FederatedCredentialRepositoryFacts
        {
            [Fact]
            public void FiltersByUserKey()
            {
                // Act
                var policies = Target.GetPoliciesCreatedByUser(userKey: 4);

                // Assert
                Assert.Equal(2, policies.Count);
                Assert.Equal(1, policies[0].Key);
                Assert.Equal(2, policies[1].Key);
            }
        }

        public class TheGetPolicyByKeyMethod : FederatedCredentialRepositoryFacts
        {
            [Fact]
            public void ReturnsPolicyByKey()
            {
                // Act
                var policy = Target.GetPolicyByKey(2);

                // Assert
                Assert.Equal(2, policy!.Key);
            }

            [Fact]
            public void ReturnsNullIfDoesNotExist()
            {
                // Act
                var policy = Target.GetPolicyByKey(23);

                // Assert
                Assert.Null(policy);
            }
        }

        public class TheSaveFederatedCredentialAsyncMethod : FederatedCredentialRepositoryFacts
        {
            [Fact]
            public async Task InsertsCredential()
            {
                // Arrange
                var credential = new FederatedCredential();

                // Act
                await Target.SaveFederatedCredentialAsync(credential, saveChanges: false);

                // Assert
                CredentialRepository.Verify(x => x.InsertOnCommit(credential), Times.Once);
                CredentialRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task CommitsChangesIfRequested()
            {
                // Arrange
                var credential = new FederatedCredential();

                // Act
                await Target.SaveFederatedCredentialAsync(credential, saveChanges: true);

                // Assert
                CredentialRepository.Verify(x => x.InsertOnCommit(credential), Times.Once);
                CredentialRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }
        }

        public class TheAddPolicyAsyncMethod : FederatedCredentialRepositoryFacts
        {
            [Fact]
            public async Task InsertsPolicy()
            {
                // Act
                await Target.AddPolicyAsync(Policies[0], saveChanges: false);

                // Assert
                PolicyRepository.Verify(x => x.InsertOnCommit(Policies[0]), Times.Once);
                PolicyRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task CommitsChangesIfRequested()
            {
                // Act
                await Target.AddPolicyAsync(Policies[0], saveChanges: true);

                // Assert
                PolicyRepository.Verify(x => x.InsertOnCommit(Policies[0]), Times.Once);
                PolicyRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }
        }

        public class TheDeletePolicyAsyncMethod : FederatedCredentialRepositoryFacts
        {
            [Fact]
            public async Task DeletesPolicy()
            {
                // Act
                await Target.DeletePolicyAsync(Policies[0], saveChanges: false);

                // Assert
                PolicyRepository.Verify(x => x.DeleteOnCommit(Policies[0]), Times.Once);
                PolicyRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task CommitsChangesIfRequested()
            {
                // Act
                await Target.DeletePolicyAsync(Policies[0], saveChanges: true);

                // Assert
                PolicyRepository.Verify(x => x.DeleteOnCommit(Policies[0]), Times.Once);
                PolicyRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }
        }

        public FederatedCredentialRepositoryFacts()
        {
            CredentialRepository = new Mock<IEntityRepository<FederatedCredential>>();
            PolicyRepository = new Mock<IEntityRepository<FederatedCredentialPolicy>>();

            Policies = new List<FederatedCredentialPolicy>
            {
                new() { Key = 1, CreatedByUserKey = 4 },
                new() { Key = 2, CreatedByUserKey = 4 },
                new() { Key = 3, CreatedByUserKey = 5 },
            };
            PolicyRepository.Setup(x => x.GetAll()).Returns(() => Policies.AsQueryable());

            Target = new FederatedCredentialRepository(
                PolicyRepository.Object,
                CredentialRepository.Object);
        }

        public Mock<IEntityRepository<FederatedCredential>> CredentialRepository { get; }
        public Mock<IEntityRepository<FederatedCredentialPolicy>> PolicyRepository { get; }
        public List<FederatedCredentialPolicy> Policies { get; }
        public FederatedCredentialRepository Target { get; }
    }
}
