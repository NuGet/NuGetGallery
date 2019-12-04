using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// This class implements the minimal function of the full <see cref="UserService"/> that we currently need for AccountDeleter
    /// This is done for now in order to not pollute DI with things that we won't use.
    /// </summary>
    public class AccountDeleteUserService : IUserService
    {
        public IEntityRepository<User> UserRepository { get; protected set; }

        public AccountDeleteUserService(
            IEntityRepository<User> userRepository)
        {
            UserRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        }

        public Task<Membership> AddMemberAsync(Organization organization, string memberName, string confirmationToken)
        {
            throw new NotImplementedException();
        }

        public Task<MembershipRequest> AddMembershipRequestAsync(Organization organization, string memberName, bool isAdmin)
        {
            throw new NotImplementedException();
        }

        public Task<Organization> AddOrganizationAsync(string username, string emailAddress, User adminUser)
        {
            throw new NotImplementedException();
        }

        public Task CancelChangeEmailAddress(User user)
        {
            throw new NotImplementedException();
        }

        public Task<User> CancelMembershipRequestAsync(Organization organization, string memberName)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CancelTransformUserToOrganizationRequest(User accountToTransform, string token)
        {
            throw new NotImplementedException();
        }

        public bool CanTransformUserToOrganization(User accountToTransform, out string errorReason)
        {
            throw new NotImplementedException();
        }

        public bool CanTransformUserToOrganization(User accountToTransform, User adminUser, out string errorReason)
        {
            throw new NotImplementedException();
        }

        public Task ChangeEmailAddress(User user, string newEmailAddress)
        {
            throw new NotImplementedException();
        }

        public Task ChangeEmailSubscriptionAsync(User user, bool emailAllowed, bool notifyPackagePushed)
        {
            throw new NotImplementedException();
        }

        public Task ChangeMultiFactorAuthentication(User user, bool enableMultiFactor, string referrer)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ConfirmEmailAddress(User user, string token)
        {
            throw new NotImplementedException();
        }

        public Task<User> DeleteMemberAsync(Organization organization, string memberName)
        {
            throw new NotImplementedException();
        }

        public IList<User> FindAllByEmailAddress(string emailAddress)
        {
            throw new NotImplementedException();
        }

        public User FindByEmailAddress(string emailAddress)
        {
            throw new NotImplementedException();
        }

        public User FindByKey(int key, bool includeDeleted = false)
        {
            throw new NotImplementedException();
        }

        public IList<User> FindByUnconfirmedEmailAddress(string unconfirmedEmailAddress, string optionalUsername)
        {
            throw new NotImplementedException();
        }

        public User FindByUsername(string username, bool includeDeleted = false)
        {
            var users = UserRepository.GetAll();
            if (!includeDeleted)
            {
                users = users.Where(u => !u.IsDeleted);
            }
            return users.Include(u => u.Roles)
                .Include(u => u.Credentials)
                .SingleOrDefault(u => u.Username == username);
        }

        public Task<IDictionary<int, string>> GetEmailAddressesForUserKeysAsync(IReadOnlyCollection<int> distinctUserKeys)
        {
            throw new NotImplementedException();
        }

        public Task RejectMembershipRequestAsync(Organization organization, string memberName, string confirmationToken)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RejectTransformUserToOrganizationRequest(User accountToTransform, User adminUser, string token)
        {
            throw new NotImplementedException();
        }

        public Task RequestTransformToOrganizationAccount(User accountToTransform, User adminUser)
        {
            throw new NotImplementedException();
        }

        public Task<bool> TransformUserToOrganization(User accountToTransform, User adminUser, string token)
        {
            throw new NotImplementedException();
        }

        public Task<Membership> UpdateMemberAsync(Organization organization, string memberName, bool isAdmin)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<User> GetSiteAdmins()
        {
            throw new NotImplementedException();
        }

        public Task SetIsAdministrator(User user, bool isAdmin)
        {
            throw new NotImplementedException();
        }
    }
}
