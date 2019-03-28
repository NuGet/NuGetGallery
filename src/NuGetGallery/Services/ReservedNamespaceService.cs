// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NuGetGallery.Auditing;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ReservedNamespaceService : IReservedNamespaceService
    {
        private static readonly Regex NamespaceRegex = new Regex(@"^\w+([_.-]\w+)*[.-]?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

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

            try
            {
                ValidateNamespace(newNamespace.Value);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(ex.Message, ex);
            }

            var matchingReservedNamespaces = FindAllReservedNamespacesForPrefix(prefix: newNamespace.Value, getExactMatches: !newNamespace.IsPrefix);
            if (matchingReservedNamespaces.Any())
            {
                throw new InvalidOperationException(Strings.ReservedNamespace_NamespaceNotAvailable);
            }

            // Mark the new namespace as shared if it matches any liberal namespace which is a shared
            // namespace. For eg: A.B.* is a shared namespace, when reserving A.B.C.* namespace, 
            // make it a shared namespace. This ensures that all namespaces under a shared 
            // namespace are also shared to keep the data consistent.
            if (!newNamespace.IsSharedNamespace && ShouldForceSharedNamespace(newNamespace.Value))
            {
                newNamespace.IsSharedNamespace = true;
            }

            ReservedNamespaceRepository.InsertOnCommit(newNamespace);
            await ReservedNamespaceRepository.CommitChangesAsync();

            await AuditingService.SaveAuditRecordAsync(
                new ReservedNamespaceAuditRecord(newNamespace, AuditedReservedNamespaceAction.ReserveNamespace));
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

                // Delete verified flags on corresponding packages for this prefix if 
                // it is the only prefix matching the package registration.
                var packageRegistrationsToMarkUnverified = namespaceToDelete
                    .PackageRegistrations
                    .Where(pr => pr.ReservedNamespaces.Count() == 1)
                    .ToList();

                if (packageRegistrationsToMarkUnverified.Any())
                {
                    await PackageService.UpdatePackageVerifiedStatusAsync(packageRegistrationsToMarkUnverified, isVerified: false);
                }

                ReservedNamespaceRepository.DeleteOnCommit(namespaceToDelete);
                await ReservedNamespaceRepository.CommitChangesAsync();

                transaction.Commit();

                await AuditingService.SaveAuditRecordAsync(
                   new ReservedNamespaceAuditRecord(namespaceToDelete, AuditedReservedNamespaceAction.UnreserveNamespace));
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

                if (namespaceToModify.Owners.Contains(userToAdd))
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.ReservedNamespace_UserAlreadyOwner, username));
                }

                Expression<Func<PackageRegistration, bool>> predicate;
                if (namespaceToModify.IsPrefix)
                {
                    predicate = registration => registration.Id.StartsWith(namespaceToModify.Value);
                }
                else
                {
                    predicate = registration => registration.Id.Equals(namespaceToModify.Value);
                }

                // Mark all packages owned by this user that start with the given namespace as verified.
                var allPackageRegistrationsForUser = userToAdd.PackageRegistrations;

                // We need 'AsQueryable' here because FindPackageRegistrationsByOwner returns an IEnumerable
                // and to evaluate the predicate server side, the casting is essential.
                var packageRegistrationsMatchingNamespace = allPackageRegistrationsForUser
                    .AsQueryable()
                    .Where(predicate)
                    .ToList();

                if (packageRegistrationsMatchingNamespace.Any())
                {
                    packageRegistrationsMatchingNamespace
                        .ForEach(pr => namespaceToModify.PackageRegistrations.Add(pr));

                    await PackageService.UpdatePackageVerifiedStatusAsync(packageRegistrationsMatchingNamespace.AsReadOnly(), isVerified: true);
                }

                namespaceToModify.Owners.Add(userToAdd);
                await ReservedNamespaceRepository.CommitChangesAsync();

                transaction.Commit();

                await AuditingService.SaveAuditRecordAsync(
                   new ReservedNamespaceAuditRecord(namespaceToModify, AuditedReservedNamespaceAction.AddOwner, username, packageRegistrationsMatchingNamespace));
            }
        }

        public async Task DeleteOwnerFromReservedNamespaceAsync(string prefix, string username, bool commitAsTransaction = true)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException(Strings.ReservedNamespace_InvalidNamespace);
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException(Strings.ReservedNamespace_InvalidUsername);
            }
            var namespaceToModify = FindReservedNamespaceForPrefix(prefix)
                   ?? throw new InvalidOperationException(string.Format(
                       CultureInfo.CurrentCulture, Strings.ReservedNamespace_NamespaceNotFound, prefix));
            List<PackageRegistration> packageRegistrationsToMarkUnverified;
            if (commitAsTransaction)
            {
                using (var strategy = new SuspendDbExecutionStrategy())
                using (var transaction = EntitiesContext.GetDatabase().BeginTransaction())
                {
                    packageRegistrationsToMarkUnverified = await DeleteOwnerFromReservedNamespaceImplAsync(prefix, username, namespaceToModify);
                    transaction.Commit();
                }
            }
            else
            {
                packageRegistrationsToMarkUnverified = await DeleteOwnerFromReservedNamespaceImplAsync(prefix, username, namespaceToModify);
            }
            await AuditingService.SaveAuditRecordAsync(
                  new ReservedNamespaceAuditRecord(namespaceToModify, AuditedReservedNamespaceAction.RemoveOwner, username, packageRegistrationsToMarkUnverified));
        }

        private async Task<List<PackageRegistration>> DeleteOwnerFromReservedNamespaceImplAsync(string prefix, string username, ReservedNamespace namespaceToModify)
        {
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

            namespaceToModify.Owners.Remove(userToRemove);

            // Remove verified mark for package registrations if the user to be removed is the only prefix owner
            // for the given package registration.
            var packageRegistrationsToMarkUnverified = packagesOwnedByUserMatchingPrefix
                .Where(pr => !pr.Owners.Any(o => 
                    ActionsRequiringPermissions.AddPackageToReservedNamespace.CheckPermissionsOnBehalfOfAnyAccount(
                        o, new[] { namespaceToModify }) == PermissionsCheckResult.Allowed))
                .ToList();

            if (packageRegistrationsToMarkUnverified.Any())
            {
                packageRegistrationsToMarkUnverified
                    .ForEach(pr => namespaceToModify.PackageRegistrations.Remove(pr));

                await PackageService.UpdatePackageVerifiedStatusAsync(packageRegistrationsToMarkUnverified, isVerified: false);
            }
            
            await ReservedNamespaceRepository.CommitChangesAsync();

            return packageRegistrationsToMarkUnverified;
        }


        /// <summary>
        /// This method fetches the reserved namespace matching the prefix and adds the 
        /// package registration entry to the reserved namespace, the provided package registration
        /// should be an entry in the database or an entity from memory to be committed. It is the caller's
        /// responsibility to commit the changes to the entity context.
        /// </summary>
        /// <param name="prefix">The prefix value of the reserved namespace to modify</param>
        /// <param name="packageRegistration">The package registration entity entry to be added.</param>
        /// <returns>Awaitable task</returns>
        public void AddPackageRegistrationToNamespace(string prefix, PackageRegistration packageRegistration)
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
        }

        /// <summary>
        /// This method fetches the reserved namespace matching the prefix and removes the 
        /// package registration entry from the reserved namespace, the provided package registration
        /// should be an entry in the database. It is the caller's responsibility to commit the 
        /// changes to the entity context.
        /// </summary>
        /// <param name="prefix">The prefix value of the reserved namespace to modify</param>
        /// <param name="packageRegistration">The package registration entity to be removed.</param>
        /// <returns>Awaitable task</returns>
        public void RemovePackageRegistrationFromNamespace(ReservedNamespace reservedNamespace, PackageRegistration packageRegistration)
        {
            if (reservedNamespace == null)
            {
                throw new ArgumentNullException(nameof(reservedNamespace));
            }

            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            reservedNamespace.PackageRegistrations.Remove(packageRegistration);
            packageRegistration.ReservedNamespaces.Remove(reservedNamespace);
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

        public virtual IReadOnlyCollection<ReservedNamespace> FindReservedNamespacesForPrefixList(IReadOnlyCollection<string> prefixList)
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

            if (value.Length > Constants.MaxPackageIdLength)
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ReservedNamespace_NamespaceExceedsLength,
                    Constants.MaxPackageIdLength));
            }

            if (!NamespaceRegex.IsMatch(value))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ReservedNamespace_InvalidCharactersInNamespace,
                    value));
            }
        }

        private bool ShouldForceSharedNamespace(string value)
        {
            var liberalMatchingNamespaces = GetReservedNamespacesForId(value);
            return liberalMatchingNamespaces.Any(rn => rn.IsSharedNamespace);
        }

        public bool ShouldMarkNewPackageIdVerified(User account, string id, out IReadOnlyCollection<ReservedNamespace> ownedMatchingReservedNamespaces)
        {
            ownedMatchingReservedNamespaces = 
                GetReservedNamespacesForId(id)
                    .Where(rn => rn.Owners.AnySafe(o => account.MatchesUser(o)))
                    .ToList()
                    .AsReadOnly();

            return ownedMatchingReservedNamespaces.Any();
        }
    }
}