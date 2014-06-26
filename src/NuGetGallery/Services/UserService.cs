using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Crypto = NuGetGallery.CryptographyService;
using NuGetGallery.Configuration;
using NuGetGallery.Auditing;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class UserService : IUserService
    {
        public IAppConfiguration Config { get; protected set; }
        public IEntityRepository<User> UserRepository { get; protected set; }
        public IEntityRepository<Credential> CredentialRepository { get; protected set; }
        public AuditingService Auditing { get; protected set; }

        protected UserService() { }

        public UserService(
            IAppConfiguration config,
            IEntityRepository<User> userRepository,
            IEntityRepository<Credential> credentialRepository,
            AuditingService auditing)
            : this()
        {
            Config = config;
            UserRepository = userRepository;
            CredentialRepository = credentialRepository;
            Auditing = auditing;
        }

        public void ChangeEmailSubscription(User user, bool emailAllowed)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            user.EmailAllowed = emailAllowed;
            UserRepository.CommitChanges();
        }

        public virtual User FindByEmailAddress(string emailAddress)
        {
            var allMatches = UserRepository.GetAll()
				.Include(u => u.Credentials)
                .Include(u => u.Roles)
                .Where(u => u.EmailAddress == emailAddress)
				.Take(2)
				.ToList();

            if (allMatches.Count == 1)
            {
                return allMatches[0];
            }

            return null;
        }

        public virtual IList<User> FindAllByEmailAddress(string emailAddress)
        {
            return UserRepository.GetAll().Where(u => u.EmailAddress == emailAddress).ToList();
        }

        public virtual IList<User> FindByUnconfirmedEmailAddress(string unconfirmedEmailAddress, string optionalUsername)
        {
            if (optionalUsername == null)
            {
                return UserRepository.GetAll().Where(u => u.UnconfirmedEmailAddress == unconfirmedEmailAddress).ToList();
            }
            else
            {
                return UserRepository.GetAll().Where(u => u.UnconfirmedEmailAddress == unconfirmedEmailAddress && u.Username == optionalUsername).ToList();
            }
        }

        public virtual User FindByUsername(string username)
        {
            return UserRepository.GetAll()
                .Include(u => u.Roles)
                .Include(u => u.Credentials)
                .SingleOrDefault(u => u.Username == username);
        }

        public async Task ChangeEmailAddress(User user, string newEmailAddress)
        {
            var existingUsers = FindAllByEmailAddress(newEmailAddress);
            if (existingUsers.AnySafe(u => u.Key != user.Key))
            {
                throw new EntityException(Strings.EmailAddressBeingUsed, newEmailAddress);
            }

            await Auditing.SaveAuditRecord(new UserAuditRecord(user, UserAuditAction.ChangeEmail, newEmailAddress));

            user.UpdateEmailAddress(newEmailAddress, Crypto.GenerateToken);
            UserRepository.CommitChanges();
        }

        public async Task CancelChangeEmailAddress(User user)
        {
            await Auditing.SaveAuditRecord(new UserAuditRecord(user, UserAuditAction.CancelChangeEmail, user.UnconfirmedEmailAddress));

            user.CancelChangeEmailAddress();
            UserRepository.CommitChanges();
        }

        public async Task<bool> ConfirmEmailAddress(User user, string token)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            if (String.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException("token");
            }

            if (user.EmailConfirmationToken != token)
            {
                return false;
            }

            var conflictingUsers = FindAllByEmailAddress(user.UnconfirmedEmailAddress);
            if (conflictingUsers.AnySafe(u => u.Key != user.Key))
            {
                throw new EntityException(Strings.EmailAddressBeingUsed, user.UnconfirmedEmailAddress);
            }

            await Auditing.SaveAuditRecord(new UserAuditRecord(user, UserAuditAction.ConfirmEmail, user.UnconfirmedEmailAddress));

            user.ConfirmEmailAddress();

            UserRepository.CommitChanges();
            return true;
        }
    }
}
