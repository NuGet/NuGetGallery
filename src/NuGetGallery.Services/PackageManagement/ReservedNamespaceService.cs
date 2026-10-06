// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class ReservedNamespaceService : IReservedNamespaceService
    {
        private static readonly Regex NamespaceRegex = RegexEx.CreateWithTimeout(
            @"^\w+([.-]\w+)*[.-]?$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

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

        public async Task ReserveNamespaceForRequestAsync(
            int requestKey,
            string namespaceValue,
            int submitterKey,
            IReadOnlyCollection<int> ownerKeys)
        {
            if (requestKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestKey));
            }

            if (submitterKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(submitterKey));
            }

            if (ownerKeys == null)
            {
                throw new ArgumentNullException(nameof(ownerKeys));
            }

            // Never retain a caller-owned collection across an await or use model-resolved owners.
            var originalOwnerKeys = ownerKeys.Distinct().ToArray();
            if (originalOwnerKeys.Length == 0 || originalOwnerKeys.Any(key => key <= 0))
            {
                throw new ArgumentException("At least one owner with a positive key is required.", nameof(ownerKeys));
            }

            if (string.IsNullOrWhiteSpace(namespaceValue)
                || namespaceValue.Length > NamespaceReservationRequestInput.MaxNamespaceLength
                || namespaceValue.Any(char.IsWhiteSpace)
                || !PackageIdValidator.IsValidPackageId(namespaceValue))
            {
                throw new ArgumentException("A valid base namespace without a wildcard or trailing dot is required.", nameof(namespaceValue));
            }

            using (new SuspendDbExecutionStrategy())
            using (var transaction = EntitiesContext.GetDatabase().BeginTransaction(IsolationLevel.Serializable))
            {
                // Tracking queries can return pre-assessment instances. Authorize only from fresh
                // database values read inside this transaction, never from their navigation properties.
                var request = EntitiesContext.Set<NamespaceReservationRequest>().AsNoTracking()
                    .SingleOrDefault(candidate => candidate.Key == requestKey);
                if (request == null
                    || !string.Equals(request.Status, "Approved", StringComparison.Ordinal)
                    || request.CompletedTimestamp == null
                    || request.SubmittedByUserKey != submitterKey
                    || !string.Equals(request.Namespace, namespaceValue, StringComparison.Ordinal)
                    || !ReadRequestedOwnerKeys(request.RequestedOwnersJson).SetEquals(originalOwnerKeys))
                {
                    throw new InvalidOperationException("The saved approval does not match the original request scope.");
                }

                var accountKeys = originalOwnerKeys.Concat(new[] { submitterKey }).Distinct().ToArray();
                var freshUsers = EntitiesContext.Users.AsNoTracking()
                    .Where(user => accountKeys.Contains(user.Key))
                    .ToDictionary(user => user.Key);
                var adminOrganizationKeys = EntitiesContext.Set<Membership>().AsNoTracking()
                    .Where(membership => membership.MemberKey == submitterKey
                        && originalOwnerKeys.Contains(membership.OrganizationKey)
                        && membership.IsAdmin)
                    .Select(membership => membership.OrganizationKey)
                    .ToList();

                if (!freshUsers.TryGetValue(submitterKey, out var submitter)
                    || !submitter.Confirmed || submitter.IsLocked || submitter.IsDeleted)
                {
                    throw new InvalidOperationException("The submitter is no longer eligible to reserve a namespace.");
                }

                foreach (var ownerKey in originalOwnerKeys)
                {
                    if (!freshUsers.TryGetValue(ownerKey, out var owner)
                        || owner.IsLocked || owner.IsDeleted
                        || (ownerKey != submitterKey
                            && (!(owner is Organization) || !adminOrganizationKeys.Contains(ownerKey))))
                    {
                        throw new InvalidOperationException("The submitter no longer has access to a requested owner.");
                    }
                }

                // Deliberately match the conservative assessment guard, including raw descendants
                // (e.g. ContosoExtra) and shared/same-owner reservations. Serializable protects both
                // query ranges until the exact namespace and dotted prefix have been committed.
                var descendants = FindAllReservedNamespacesForPrefix(namespaceValue, getExactMatches: false);
                var parents = GetReservedNamespacesForId(namespaceValue);
                if (descendants.Any() || parents.Any())
                {
                    throw new InvalidOperationException(ServicesStrings.ReservedNamespace_NamespaceNotAvailable);
                }

                // Use tracked entities only to create relationships, after fresh key-based authorization.
                var trackedOwners = EntitiesContext.Users
                    .Where(user => originalOwnerKeys.Contains(user.Key))
                    .ToDictionary(user => user.Key);
                if (trackedOwners.Count != originalOwnerKeys.Length)
                {
                    throw new InvalidOperationException("A requested owner is no longer available.");
                }

                var namespaces = new[]
                {
                    new ReservedNamespace(namespaceValue, isSharedNamespace: false, isPrefix: false),
                    new ReservedNamespace(namespaceValue + ".", isSharedNamespace: false, isPrefix: true)
                };
                var ownerPackages = new Dictionary<ReservedNamespace, Dictionary<int, List<PackageRegistration>>>();
                foreach (var reservedNamespace in namespaces)
                {
                    ReservedNamespaceRepository.InsertOnCommit(reservedNamespace);
                    var matchesByOwner = new Dictionary<int, List<PackageRegistration>>();
                    ownerPackages.Add(reservedNamespace, matchesByOwner);
                    foreach (var ownerKey in originalOwnerKeys)
                    {
                        var owner = trackedOwners[ownerKey];
                        Expression<Func<PackageRegistration, bool>> predicate;
                        if (reservedNamespace.IsPrefix)
                        {
                            predicate = registration => registration.Id.StartsWith(reservedNamespace.Value);
                        }
                        else
                        {
                            predicate = registration => registration.Id.Equals(reservedNamespace.Value);
                        }

                        var matches = PackageService.FindPackageRegistrationsByOwner(owner)
                            .AsQueryable().Where(predicate).ToList()
                            .GroupBy(registration => registration.Key).Select(group => group.First()).ToList();
                        matchesByOwner.Add(ownerKey, matches);
                        reservedNamespace.Owners.Add(owner);
                    }

                    foreach (var registration in matchesByOwner.Values.SelectMany(matches => matches)
                        .GroupBy(registration => registration.Key).Select(group => group.First()))
                    {
                        reservedNamespace.PackageRegistrations.Add(registration);
                    }
                }

                var packages = namespaces.SelectMany(reservedNamespace => reservedNamespace.PackageRegistrations)
                    .GroupBy(registration => registration.Key).Select(group => group.First()).ToArray();
                if (packages.Length > 0)
                {
                    await PackageService.UpdatePackageVerifiedStatusAsync(packages, isVerified: true, commitChanges: false);
                }

                // One shared-context save for both reservations, relationships and verified flags.
                // Do not modify the approval or retry/compensate this unit of work on any failure.
                await ReservedNamespaceRepository.CommitChangesAsync();
                transaction.Commit();

                foreach (var reservedNamespace in namespaces)
                {
                    await AuditingService.SaveAuditRecordAsync(
                        new ReservedNamespaceAuditRecord(reservedNamespace, AuditedReservedNamespaceAction.ReserveNamespace));
                    foreach (var ownerKey in originalOwnerKeys)
                    {
                        await AuditingService.SaveAuditRecordAsync(
                            new ReservedNamespaceAuditRecord(reservedNamespace, AuditedReservedNamespaceAction.AddOwner,
                                freshUsers[ownerKey].Username, ownerPackages[reservedNamespace][ownerKey]));
                    }
                }
            }
        }

        private static HashSet<int> ReadRequestedOwnerKeys(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("The saved owner scope is invalid.");
            }

            JArray owners;
            try
            {
                owners = JArray.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("The saved owner scope is invalid.", ex);
            }

            var keys = new HashSet<int>();
            foreach (var owner in owners)
            {
                var key = (owner as JObject)?["Key"];
                if (key == null || key.Type != JTokenType.Integer
                    || !int.TryParse(key.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var ownerKey)
                    || ownerKey <= 0)
                {
                    throw new InvalidOperationException("The saved owner scope is invalid.");
                }

                keys.Add(ownerKey);
            }

            if (keys.Count == 0)
            {
                throw new InvalidOperationException("The saved owner scope is empty.");
            }

            return keys;
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
                throw new InvalidOperationException(ServicesStrings.ReservedNamespace_NamespaceNotAvailable);
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
                throw new ArgumentException(ServicesStrings.ReservedNamespace_InvalidNamespace);
            }

            using (new SuspendDbExecutionStrategy())
            using (var transaction = EntitiesContext.GetDatabase().BeginTransaction())
            {
                var namespaceToDelete = FindReservedNamespaceForPrefix(existingNamespace)
                    ?? throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture, ServicesStrings.ReservedNamespace_NamespaceNotFound, existingNamespace));

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
                throw new ArgumentException(ServicesStrings.ReservedNamespace_InvalidNamespace);
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException(ServicesStrings.ReservedNamespace_InvalidUsername);
            }

            using (var strategy = new SuspendDbExecutionStrategy())
            using (var transaction = EntitiesContext.GetDatabase().BeginTransaction())
            {
                var namespaceToModify = FindReservedNamespaceForPrefix(prefix)
                    ?? throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture, ServicesStrings.ReservedNamespace_NamespaceNotFound, prefix));

                var userToAdd = UserService.FindByUsername(username)
                    ?? throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture, ServicesStrings.ReservedNamespace_UserNotFound, username));

                if (namespaceToModify.Owners.Contains(userToAdd))
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, ServicesStrings.ReservedNamespace_UserAlreadyOwner, username));
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
                var allPackageRegistrationsForUser = PackageService.FindPackageRegistrationsByOwner(userToAdd);

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

        public async Task DeleteOwnerFromReservedNamespaceAsync(string prefix, string username, bool commitChanges = true)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException(ServicesStrings.ReservedNamespace_InvalidNamespace);
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException(ServicesStrings.ReservedNamespace_InvalidUsername);
            }
            var namespaceToModify = FindReservedNamespaceForPrefix(prefix)
                   ?? throw new InvalidOperationException(string.Format(
                       CultureInfo.CurrentCulture, ServicesStrings.ReservedNamespace_NamespaceNotFound, prefix));
            List<PackageRegistration> packageRegistrationsToMarkUnverified;
            if (commitChanges)
            {
                using (new SuspendDbExecutionStrategy())
                using (var transaction = EntitiesContext.GetDatabase().BeginTransaction())
                {
                    packageRegistrationsToMarkUnverified = await DeleteOwnerFromReservedNamespaceImplAsync(prefix, username, namespaceToModify);
                    transaction.Commit();
                }
            }
            else
            {
                packageRegistrationsToMarkUnverified = await DeleteOwnerFromReservedNamespaceImplAsync(prefix, username, namespaceToModify, commitChanges: false);
            }

            await AuditingService.SaveAuditRecordAsync(
                  new ReservedNamespaceAuditRecord(namespaceToModify, AuditedReservedNamespaceAction.RemoveOwner, username, packageRegistrationsToMarkUnverified));
        }

        private async Task<List<PackageRegistration>> DeleteOwnerFromReservedNamespaceImplAsync(string prefix, string username, ReservedNamespace namespaceToModify, bool commitChanges = true)
        {
            var userToRemove = UserService.FindByUsername(username)
                ?? throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture, ServicesStrings.ReservedNamespace_UserNotFound, username));

            if (!namespaceToModify.Owners.Contains(userToRemove))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, ServicesStrings.ReservedNamespace_UserNotAnOwner, username));
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

                await PackageService.UpdatePackageVerifiedStatusAsync(packageRegistrationsToMarkUnverified, isVerified: false, commitChanges: false);
            }
            
            if (commitChanges)
            {
                await ReservedNamespaceRepository.CommitChangesAsync();
            }

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
                throw new ArgumentException(ServicesStrings.ReservedNamespace_InvalidNamespace);
            }

            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            var namespaceToModify = FindReservedNamespaceForPrefix(prefix)
                ?? throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture, ServicesStrings.ReservedNamespace_NamespaceNotFound, prefix));

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
                throw new ArgumentException(ServicesStrings.ReservedNamespace_InvalidNamespace);
            }

            if (value.Length > Constants.MaxPackageIdLength)
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    ServicesStrings.ReservedNamespace_NamespaceExceedsLength,
                    Constants.MaxPackageIdLength));
            }

            if (!NamespaceRegex.IsMatch(value))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    ServicesStrings.ReservedNamespace_InvalidCharactersInNamespace,
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