// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IUserService
    {
        Task<MembershipRequest> AddMembershipRequestAsync(Organization organization, string memberName, bool isAdmin);

        Task RejectMembershipRequestAsync(Organization organization, string memberName, string confirmationToken);

        Task<User> CancelMembershipRequestAsync(Organization organization, string memberName);

        Task<Membership> AddMemberAsync(Organization organization, string memberName, string confirmationToken);

        Task<Membership> UpdateMemberAsync(Organization organization, string memberName, bool isAdmin);

        Task<User> DeleteMemberAsync(Organization organization, string memberName);

        Task ChangeEmailSubscriptionAsync(User user, bool emailAllowed, bool notifyPackagePushed);

        User FindByEmailAddress(string emailAddress);

        IList<User> FindAllByEmailAddress(string emailAddress);

        IList<User> FindByUnconfirmedEmailAddress(string unconfirmedEmailAddress, string optionalUsername);

        User FindByUsername(string username, bool includeDeleted = false);

        User FindByKey(int key, bool includeDeleted = false);

        Task<bool> ConfirmEmailAddress(User user, string token);

        Task ChangeEmailAddress(User user, string newEmailAddress);

        Task CancelChangeEmailAddress(User user);

        Task ChangeMultiFactorAuthentication(User user, bool enableMultiFactor, string referrer = null);

        Task<IDictionary<int, string>> GetEmailAddressesForUserKeysAsync(IReadOnlyCollection<int> distinctUserKeys);

        bool CanTransformUserToOrganization(User accountToTransform, out string errorReason);

        bool CanTransformUserToOrganization(User accountToTransform, User adminUser, out string errorReason);

        Task RequestTransformToOrganizationAccount(User accountToTransform, User adminUser);
        
        Task<bool> TransformUserToOrganization(User accountToTransform, User adminUser, string token);

        Task<bool> RejectTransformUserToOrganizationRequest(User accountToTransform, User adminUser, string token);

        Task<bool> CancelTransformUserToOrganizationRequest(User accountToTransform, string token);

        Task<Organization> AddOrganizationAsync(string username, string emailAddress, User adminUser);

        IReadOnlyList<User> GetSiteAdmins();

        Task SetIsAdministrator(User user, bool isAdmin);
    }
}