// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging;
using NuGetGallery.Auditing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class ReservedNamespaceService : IReservedNamespaceService
    {
        private static readonly Regex NamespaceRegex = new Regex(@"^\w+([_.-]\w+)*[.]?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

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
            AuditingService = auditing;
        }

        public async Task AddReservedNamespaceAsync(ReservedNamespace newNamespace)
        {
            if (newNamespace == null)
            {
                throw new ArgumentNullException(nameof(newNamespace));
            }

            ValidateNamespace(newNamespace.Value);

            var matchingReservedNamespaces = FindAllReservedNamespacesForPrefix(prefix: newNamespace.Value, getExactMatches: !newNamespace.IsPrefix);
            if (matchingReservedNamespaces.Any())
            {
                throw new InvalidOperationException(Strings.ReservedNamespace_NamespaceNotAvailable);
            }

            ReservedNamespaceRepository.InsertOnCommit(newNamespace);
            await ReservedNamespaceRepository.CommitChangesAsync();
        }

        public async Task DeleteReservedNamespaceAsync(string existingNamespace)
        {
            if (string.IsNullOrWhiteSpace(existingNamespace))
            {
                throw new ArgumentException(Strings.ReservedNamespace_InvalidNamespace);
            }

            using (var strategy = new SuspendDbExecutionStrategy())
            using (var transaction = EntitiesContext.GetDatabase().BeginTransaction())
            {
                var namespaceToDelete = FindReservedNamespaceForPrefix(existingNamespace)
                    ?? throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture, Strings.ReservedNamespace_NamespaceNotFound, existingNamespace));

                // Delete verified flags on corresponding packages for this prefix if it is the only prefix matching the 
                // package registration.
                if (!namespaceToDelete.IsSharedNamespace)
                {
                    var packageRegistrationsToMarkUnverified = namespaceToDelete
                        .PackageRegistrations
                        .Where(pr => pr.ReservedNamespaces.Count() == 1)
                        .ToList();

                    if (packageRegistrationsToMarkUnverified.Any())
                    {
                        await PackageService.UpdatePackageVerifiedStatusAsync(packageRegistrationsToMarkUnverified, isVerified: false);
                    }
                }

                ReservedNamespaceRepository.DeleteOnCommit(namespaceToDelete);
                await ReservedNamespaceRepository.CommitChangesAsync();

                transaction.Commit();
            }
        }

        public async Task AddOwnerToReservedNamespaceAsync(string prefix, string username)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException(Strings.ReservedNamespace_InvalidNamespace);
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException(Strings.ReservedNamespace_InvalidUsername);
            }

            using (var strategy = new SuspendDbExecutionStrategy())
            using (var transaction = EntitiesContext.GetDatabase().BeginTransaction())
            {
                var namespaceToModify = FindReservedNamespaceForPrefix(prefix)
                    ?? throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture, Strings.ReservedNamespace_NamespaceNotFound, prefix));

                var userToAdd = UserService.FindByUsername(username)
                    ?? throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture, Strings.ReservedNamespace_UserNotFound, username));

                // Mark all packages owned by this user that start with the given namespace as verified.
                var allPackageRegistrationsForUser = PackageService.FindPackageRegistrationsByOwner(userToAdd);
                var packageRegistrationsMatchingNamespace = allPackageRegistrationsForUser
                    .Where(pr => pr.Id.StartsWith(namespaceToModify.Value, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (packageRegistrationsMatchingNamespace.Any())
                {
                    packageRegistrationsMatchingNamespace
                        .ForEach(pr => namespaceToModify.PackageRegistrations.Add(pr));

                    await PackageService.UpdatePackageVerifiedStatusAsync(packageRegistrationsMatchingNamespace, isVerified: true);
                }

                namespaceToModify.Owners.Add(userToAdd);
                await ReservedNamespaceRepository.CommitChangesAsync();

                transaction.Commit();
            }
        }

        public async Task DeleteOwnerFromReservedNamespaceAsync(string prefix, string username)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException(Strings.ReservedNamespace_InvalidNamespace);
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException(Strings.ReservedNamespace_InvalidUsername);
            }

            using (var strategy = new SuspendDbExecutionStrategy())
            using (var transaction = EntitiesContext.GetDatabase().BeginTransaction())
            {
                var namespaceToModify = FindReservedNamespaceForPrefix(prefix)
                    ?? throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture, Strings.ReservedNamespace_NamespaceNotFound, prefix));

                var userToRemove = UserService.FindByUsername(username)
                    ?? throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture, Strings.ReservedNamespace_UserNotFound, username));

                if (!namespaceToModify.Owners.Contains(userToRemove))
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.ReservedNamespace_UserNotAnOwner, username));
                }

                var packagesOwnedByUserMatchingPrefix = namespaceToModify
                        .PackageRegistrations
                        .Where(pr => pr
                            .Owners
                            .Any(pro => pro.Username == userToRemove.Username))
                        .ToList();

                // Remove verified mark for package registrations if the user to be removed is the only prefix owner
                // for the given package registration.
                var packageRegistrationsToMarkUnverified = packagesOwnedByUserMatchingPrefix
                    .Where(pr => pr.Owners.Intersect(namespaceToModify.Owners).Count() == 1)
                    .ToList();

                if (packageRegistrationsToMarkUnverified.Any())
                {
                    packageRegistrationsToMarkUnverified
                        .ForEach(pr => namespaceToModify.PackageRegistrations.Remove(pr));

                    await PackageService.UpdatePackageVerifiedStatusAsync(packageRegistrationsToMarkUnverified, isVerified: false);
                }

                namespaceToModify.Owners.Remove(userToRemove);
                await ReservedNamespaceRepository.CommitChangesAsync();

                transaction.Commit();
            }
        }

        /// <summary>
        /// This method fetches the reserved namespace matching the prefix and adds the 
        /// package registration entry to the reserved namespace, the provided package registration
        /// should be an entry in the database or an entity from memory to be committed.
        /// </summary>
        /// <param name="prefix">The prefix value of the reserved namespace to modify</param>
        /// <param name="packageRegistration">The package registration entity entry to be added.</param>
        /// <param name="commitChanges">Flag to commit the modifications to the database, if set to false
        /// the caller of this method should take care of saving changes for entities context.</param>
        /// <returns>Awaitable task</returns>
        public async Task AddPackageRegistrationToNamespaceAsync(string prefix, PackageRegistration packageRegistration, bool commitChanges = true)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException(Strings.ReservedNamespace_InvalidNamespace);
            }

            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            var namespaceToModify = FindReservedNamespaceForPrefix(prefix)
                ?? throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture, Strings.ReservedNamespace_NamespaceNotFound, prefix));

            namespaceToModify.PackageRegistrations.Add(packageRegistration);

            if (commitChanges)
            {
                await ReservedNamespaceRepository.CommitChangesAsync();
            }
        }

        public virtual ReservedNamespace FindReservedNamespaceForPrefix(string prefix)
        {
            return (from request in ReservedNamespaceRepository.GetAll()
                    where request.Value.Equals(prefix)
                    select request).FirstOrDefault();
        }

        public virtual IReadOnlyCollection<ReservedNamespace> FindAllReservedNamespacesForPrefix(string prefix, bool getExactMatches)
        {
            Expression<Func<ReservedNamespace, bool>> prefixMatch;
            if (getExactMatches)
            {
                prefixMatch = dbPrefix => dbPrefix.Value.Equals(prefix);
            }
            else
            {
                prefixMatch = dbPrefix => dbPrefix.Value.StartsWith(prefix);
            }

            return ReservedNamespaceRepository.GetAll()
                .Where(prefixMatch)
                .ToList();
        }

        public IReadOnlyCollection<ReservedNamespace> FindReservedNamespacesForPrefixList(IReadOnlyCollection<string> prefixList)
        {
            return (from dbPrefix in ReservedNamespaceRepository.GetAll()
                    join queryPrefix in prefixList
                    on dbPrefix.Value equals queryPrefix
                    select dbPrefix).ToList();
        }

        public virtual IReadOnlyCollection<ReservedNamespace> GetReservedNamespacesForId(string id)
        {
            return (from request in ReservedNamespaceRepository.GetAll()
                    where (request.IsPrefix && id.StartsWith(request.Value))
                        || (!request.IsPrefix && id.Equals(request.Value))
                    select request).ToList();
        }

        public static void ValidateNamespace(string value)
        {
            // Same restrictions as that of NuGetGallery.Core.Packaging.PackageIdValidator except for the regex change, a namespace could end in a '.'
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(Strings.ReservedNamespace_InvalidNamespace);
            }

            if (value.Length > CoreConstants.MaxPackageIdLength)
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ReservedNamespace_NamespaceExceedsLength,
                    CoreConstants.MaxPackageIdLength));
            }

            if (!NamespaceRegex.IsMatch(value))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ReservedNamespace_InvalidCharactersInNamespace,
                    value));
            }
        }

        public bool IsPushAllowed(string id, User user, out IReadOnlyCollection<ReservedNamespace> userOwnedMatchingNamespaces)
        {
            // Allow push to a new package ID only if
            // 1. There is no namespace match for the given ID
            // 2. Or one of the matching namespace is a shared namespace.
            // 3. Or the current user is one of the owner of a matching namespace.
            var matchingNamespaces = GetReservedNamespacesForId(id);
            var noNamespaceMatches = matchingNamespaces.Count() == 0;
            var idMatchesSharedNamespace = matchingNamespaces.Any(rn => rn.IsSharedNamespace);
            userOwnedMatchingNamespaces = matchingNamespaces
                .Where(rn => rn.Owners.AnySafe(o => o.Username == user.Username))
                .ToList()
                .AsReadOnly();

            return noNamespaceMatches || idMatchesSharedNamespace || userOwnedMatchingNamespaces.Any();
        }
    }
}