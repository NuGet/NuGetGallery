// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IUserService
    {
        Task ChangeEmailSubscriptionAsync(User user, bool emailAllowed, bool notifyPackagePushed);

        User FindByEmailAddress(string emailAddress);

        IList<User> FindAllByEmailAddress(string emailAddress);

        IList<User> FindByUnconfirmedEmailAddress(string unconfirmedEmailAddress, string optionalUsername);

        User FindByUsername(string username);

        User FindByKey(int key);

        Task<bool> ConfirmEmailAddress(User user, string token);

        Task ChangeEmailAddress(User user, string newEmailAddress);

        Task CancelChangeEmailAddress(User user);

        Task<IDictionary<int, string>> GetEmailAddressesForUserKeysAsync(IReadOnlyCollection<int> distinctUserKeys);

        bool CanTransformUserToOrganization(User accountToTransform, out string errorReason);
        
        Task RequestTransformToOrganizationAccount(User accountToTransform, User adminUser);
        
        Task<bool> TransformUserToOrganization(User accountToTransform, User adminUser, string token);
    }
}