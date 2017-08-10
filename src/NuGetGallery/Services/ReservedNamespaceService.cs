﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Auditing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class ReservedNamespaceService : IReservedNamespaceService
    {
        public IEntitiesContext EntitiesContext { get; protected set; }
        public IEntityRepository<ReservedNamespace> ReservedNamespaceRepository { get; protected set; }
        public IUserService UserService { get; protected set; }
        public IPackageService PackageService { get; protected set; }
        public IAuditingService AuditingService { get; protected set; }

        protected ReservedNamespaceService() { }

        public ReservedNamespaceService(
            IEntitiesContext entitiesContext,
            IEntityRepository<ReservedNamespace> reservedNamespaceRepository,
            IUserService userService,
            IPackageService packageService,
            IAuditingService auditing)
            : this()
        {
            EntitiesContext = entitiesContext;
            ReservedNamespaceRepository = reservedNamespaceRepository;
            UserService = userService;
            PackageService = packageService;
            // TODO: Add auditing
            AuditingService = auditing;
        }

        public async Task AddReservedNamespaceAsync(ReservedNamespace newNamespace)
        {
            if (newNamespace == null)
            {
                throw new ArgumentNullException(nameof(newNamespace));
            }

            var matchingReservedNamespaces = FindAllReservedNamespacesForPrefix(newNamespace.Value, !newNamespace.IsPrefix);
            if (matchingReservedNamespaces.Count() > 0)
            {
                throw new InvalidOperationException($"The specified namespace is already reserved or is a more liberal namespace.");
            }

            ReservedNamespaceRepository.InsertOnCommit(newNamespace);
            await ReservedNamespaceRepository.CommitChangesAsync();
        }

        public async Task DeleteReservedNamespaceAsync(ReservedNamespace existingNamespace)
        {
            if (existingNamespace == null)
            {
                throw new ArgumentNullException(nameof(existingNamespace));
            }

            EntitiesConfiguration.SuspendExecutionStrategy = true;
            using (var transaction = EntitiesContext.GetDatabase().BeginTransaction())
            {
                var namespaceToDelete = FindReservedNamespaceForPrefix(existingNamespace.Value);
                if (namespaceToDelete == null)
                {
                    throw new InvalidOperationException($"Namespace '{existingNamespace.Value}' not found.");
                }

                // Delete verified flags on corresponding packages for this prefix if it is the only prefix matching the 
                // package registration.
                if (namespaceToDelete.IsSharedNamespace == false)
                {
                    var packageRegistrationsToMarkUnVerified = namespaceToDelete
                        .PackageRegistrations
                        .Where(pr => pr.ReservedNamespaces.Count() == 1)
                        .ToList();

                    if (packageRegistrationsToMarkUnVerified.Count() > 0)
                    {
                        await PackageService.UpdatePackageVerifiedStatusAsync(packageRegistrationsToMarkUnVerified, isVerified: false);
                    }
                }

                ReservedNamespaceRepository.DeleteOnCommit(namespaceToDelete);
                await ReservedNamespaceRepository.CommitChangesAsync();

                transaction.Commit();
            }

            EntitiesConfiguration.SuspendExecutionStrategy = false;
        }

        public async Task AddOwnerToReservedNamespaceAsync(ReservedNamespace prefix, User user)
        {
            if (prefix == null)
            {
                throw new ArgumentNullException(nameof(prefix));
            }

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            EntitiesConfiguration.SuspendExecutionStrategy = true;
            using (var transaction = EntitiesContext.GetDatabase().BeginTransaction())
            {
                var namespaceToModify = FindReservedNamespaceForPrefix(prefix.Value);
                if (namespaceToModify == null)
                {
                    throw new InvalidOperationException($"Namespace '{prefix.Value}' not found.");
                }

                var userToAdd = UserService.FindByUsername(user.Username);
                if (userToAdd == null)
                {
                    throw new InvalidOperationException($"User not found with username: {user.Username}");
                }

                // Find all packages owned by this user which starts with the given namespace to be marked as verified.
                var allPackageRegistrationsForUser = PackageService.FindPackageRegistrationsByOwner(userToAdd);
                var packageRegistrationsMatchingNamespace = allPackageRegistrationsForUser
                    .Where(pr => pr.Id.StartsWith(namespaceToModify.Value, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (packageRegistrationsMatchingNamespace.Count > 0)
                {
                    packageRegistrationsMatchingNamespace
                        .ForEach(pr => namespaceToModify.PackageRegistrations.Add(pr));

                    await PackageService.UpdatePackageVerifiedStatusAsync(packageRegistrationsMatchingNamespace, isVerified: true);
                }

                namespaceToModify.Owners.Add(userToAdd);
                await ReservedNamespaceRepository.CommitChangesAsync();

                transaction.Commit();
            }

            EntitiesConfiguration.SuspendExecutionStrategy = false;
        }

        public async Task DeleteOwnerFromReservedNamespaceAsync(ReservedNamespace prefix, User user)
        {
            if (prefix == null)
            {
                throw new ArgumentNullException(nameof(prefix));
            }

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            EntitiesConfiguration.SuspendExecutionStrategy = true;
            using (var transaction = EntitiesContext.GetDatabase().BeginTransaction())
            {
                var namespaceToModify = FindReservedNamespaceForPrefix(prefix.Value);
                if (namespaceToModify == null)
                {
                    throw new InvalidOperationException($"Namespace '{prefix.Value}' not found.");
                }

                var userToRemove = UserService.FindByUsername(user.Username);
                if (userToRemove == null)
                {
                    throw new InvalidOperationException($"User not found with username: {user.Username}");
                }

                if (!namespaceToModify.Owners.Contains(userToRemove))
                {
                    throw new InvalidOperationException($"User {user.Username} is not an owner of this namespace.");
                }

                var packagesOwnedByUserMatchingPrefix = namespaceToModify
                        .PackageRegistrations
                        .Where(pr => pr
                            .Owners
                            .Any(pro => pro.Username == userToRemove.Username))
                        .ToList();

                // Remove verified mark for package registrations if the user to be removed is the only prefix owner
                // for the given package registration.
                var removeVerifiedMarksForPackages = packagesOwnedByUserMatchingPrefix
                    .Where(pr => pr.Owners.Intersect(namespaceToModify.Owners).Count() == 1)
                    .ToList();

                if (removeVerifiedMarksForPackages.Count > 0)
                {
                    removeVerifiedMarksForPackages
                        .ForEach(pr => namespaceToModify.PackageRegistrations.Remove(pr));

                    await PackageService.UpdatePackageVerifiedStatusAsync(removeVerifiedMarksForPackages, isVerified: false);
                }

                namespaceToModify.Owners.Remove(userToRemove);
                await ReservedNamespaceRepository.CommitChangesAsync();

                transaction.Commit();
            }

            EntitiesConfiguration.SuspendExecutionStrategy = false;
        }

        public ReservedNamespace FindReservedNamespaceForPrefix(string prefix)
        {
            return (from request in ReservedNamespaceRepository.GetAll()
                    where request.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                    select request).FirstOrDefault();
        }

        public IList<ReservedNamespace> FindAllReservedNamespacesForPrefix(string prefix, bool getExactMatches)
        {
            Expression<Func<ReservedNamespace, bool>> prefixMatch =
                dbPrefix => getExactMatches
                    ? dbPrefix.Value.Equals(prefix)
                    : dbPrefix.Value.StartsWith(prefix);

            return ReservedNamespaceRepository.GetAll()
                .Where(prefixMatch)
                .ToList();
        }

        public IList<ReservedNamespace> FindReservedNamespacesForPrefixList(IList<string> prefixList)
        {
            return (from dbPrefix in ReservedNamespaceRepository.GetAll()
                    join queryPrefix in prefixList
                    on dbPrefix.Value.ToLower() equals queryPrefix.ToLower()
                    select dbPrefix).ToList();
        }

        public IList<ReservedNamespace> GetReservedNamespacesForId(string id)
        {
            return (from request in ReservedNamespaceRepository.GetAll()
                    where id.StartsWith(request.Value, StringComparison.OrdinalIgnoreCase)
                    select request).ToList();
        }
    }
}