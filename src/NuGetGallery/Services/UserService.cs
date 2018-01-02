// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using NuGetGallery.Configuration;
using NuGetGallery.Auditing;
using System.Threading.Tasks;
using NuGetGallery.Security;
using Crypto = NuGetGallery.CryptographyService;
using System.Data.SqlClient;
using System.Data;

namespace NuGetGallery
{
    public class UserService : IUserService
    {
        public IAppConfiguration Config { get; protected set; }
        public IEntityRepository<User> UserRepository { get; protected set; }
        public IEntityRepository<Credential> CredentialRepository { get; protected set; }
        public IAuditingService Auditing { get; protected set; }
        public IEntitiesContext EntitiesContext { get; protected set; }

        protected UserService() { }

        public UserService(
            IAppConfiguration config,
            IEntityRepository<User> userRepository,
            IEntityRepository<Credential> credentialRepository,
            IAuditingService auditing,
            IEntitiesContext entitiesContext)
            : this()
        {
            Config = config;
            UserRepository = userRepository;
            CredentialRepository = credentialRepository;
            Auditing = auditing;
            EntitiesContext = entitiesContext;
        }

        public async Task ChangeEmailSubscriptionAsync(User user, bool emailAllowed, bool notifyPackagePushed)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.EmailAllowed = emailAllowed;
            user.NotifyPackagePushed = notifyPackagePushed;
            await UserRepository.CommitChangesAsync();
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

            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, AuditedUserAction.ChangeEmail, newEmailAddress));

            user.UpdateEmailAddress(newEmailAddress, Crypto.GenerateToken);
            await UserRepository.CommitChangesAsync();
        }

        public async Task CancelChangeEmailAddress(User user)
        {
            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, AuditedUserAction.CancelChangeEmail, user.UnconfirmedEmailAddress));

            user.CancelChangeEmailAddress();
            await UserRepository.CommitChangesAsync();
        }

        public async Task<IDictionary<int, string>> GetEmailAddressesForUserKeysAsync(IReadOnlyCollection<int> distinctUserKeys)
        {
            var results = await UserRepository.GetAll()
                .Where(u => distinctUserKeys.Contains(u.Key))
                .Select(u => new { u.Key, u.EmailAddress })
                .ToDictionaryAsync(u => u.Key, u => u.EmailAddress);

            return results;
        }

        public async Task<bool> ConfirmEmailAddress(User user, string token)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (String.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException(nameof(token));
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

            await Auditing.SaveAuditRecordAsync(new UserAuditRecord(user, AuditedUserAction.ConfirmEmail, user.UnconfirmedEmailAddress));

            user.ConfirmEmailAddress();

            await UserRepository.CommitChangesAsync();
            return true;
        }

        private const string ExecMigrateToOrganization = "EXEC [dbo].[MigrateToOrganization] @orgKey @adminKey @token";
        
        public async Task TransformToOrganizationAccount(User accountToTransform, User adminUser, string token)
        {
            accountToTransform = accountToTransform ?? throw new ArgumentNullException(nameof(accountToTransform));
            adminUser = adminUser?? throw new ArgumentNullException(nameof(adminUser));
            
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentNullException(nameof(token));
            }

            var tenantId = adminUser.GetTenantId();
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                // todo: add security policy to organization to enforce this (future work)
                throw new TransformAccountException(Strings.TransformAccount_AdminDoesNotHaveTenantId);
            }

            // Update from User to Organization account. Note that the type change will only be reflected in future
            // requests, which use new EF context instances.
            try
            {
                var database = EntitiesContext.GetDatabase();
                var result = await database.ExecuteSqlCommandAsync(
                    ExecMigrateToOrganization,
                    new SqlParameter("organizationKey", accountToTransform.Key),
                    new SqlParameter("adminKey", adminUser.Key),
                    new SqlParameter("token", token)
                    );

                if (result == 0)
                {
                    // Stored procedure returned failure, probably due to an unsatisfied migration request.
                    throw new TransformAccountException(Strings.TransformAccount_SaveFailed);
                }
            }
            catch (Exception ex) when (ex is SqlException || ex is DataException)
            {
                // EF exception when saving account transformation to the database.
                throw new TransformAccountException(Strings.TransformAccount_DatabaseError);
            }
        }
    }
}
