using NuGetGallery.Auditing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Web;

namespace NuGetGallery
{
    public class ReservedNamespaceService : IReservedNamespaceService
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IEntityRepository<ReservedNamespace> _reservedNamespaceRepository;
        private readonly IUserService _userService;
        private readonly IPackageService _packageService;
        private readonly IAuditingService _auditingService;

        protected ReservedNamespaceService() { }

        public ReservedNamespaceService(
            IEntitiesContext entitiesContext,
            IEntityRepository<ReservedNamespace> reservedNamespaceRepository,
            IUserService userService,
            IPackageService packageService,
            IAuditingService auditing)
            : this()
        {
            _entitiesContext = entitiesContext;
            _reservedNamespaceRepository = reservedNamespaceRepository;
            _userService = userService;
            _packageService = packageService;
            // TODO: Add auditing everywhere
            _auditingService = auditing;
        }

        public async Task AddReservedNamespaceAsync(ReservedNamespace prefix)
        {
            if (prefix == null)
            {
                throw new ArgumentNullException(nameof(prefix));
            }

            var matchingReservedNamespaces = FindAllReservedNamespacesForPrefix(prefix.Value, !prefix.IsPrefix);
            if (matchingReservedNamespaces.Count() > 0)
            {
                throw new InvalidOperationException($"The specified namespace is already reserved or is a more liberal namespace.");
            }

            _reservedNamespaceRepository.InsertOnCommit(prefix);
            await _reservedNamespaceRepository.CommitChangesAsync();
        }

        public async Task DeleteReservedNamespaceAsync(ReservedNamespace prefix)
        {
            if (prefix == null)
            {
                throw new ArgumentNullException(nameof(prefix));
            }

            EntitiesConfiguration.SuspendExecutionStrategy = true;
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                var namespaceToDelete = FindReservedNamespaceForPrefix(prefix.Value);
                if (namespaceToDelete == null)
                {
                    throw new InvalidOperationException($"Namespace '{prefix.Value}' not found.");
                }

                // Delete verified tags on corresponding packages for this prefix if it is the only prefix matching the 
                // package registration or the only prefix with no shared namespace.
                if (namespaceToDelete.IsSharedNamespace == false)
                {
                    // Double check for cases where multiple namespaces for a given PR but all could be shared namespace
                    var packageRegistrationsToMarkUnVerified = namespaceToDelete
                        .PackageRegistrations
                        .Where(pr => pr.ReservedNamespaces.Count() == 1)
                        .ToList();

                    await _packageService.UpdatePackageVerifiedStatusAsync(packageRegistrationsToMarkUnVerified, isVerified: false);
                }

                _reservedNamespaceRepository.DeleteOnCommit(namespaceToDelete);
                await _reservedNamespaceRepository.CommitChangesAsync();

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
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                var namespaceToModify = FindReservedNamespaceForPrefix(prefix.Value);
                if (namespaceToModify == null)
                {
                    throw new InvalidOperationException($"Namespace '{prefix.Value}' not found.");
                }

                var userToAdd = _userService.FindByUsername(user.Username);
                if (userToAdd == null)
                {
                    throw new InvalidOperationException($"User not found with username: {user.Username}");
                }

                if (!namespaceToModify.IsSharedNamespace)
                {
                    // Find all packages owned by this user which starts with the given namespace to be marked as verified.
                    var allPackageRegistrationsForUser = _packageService.FindPackageRegistrationsByOwner(userToAdd);
                    var packageRegistrationsMatchingNamespace = allPackageRegistrationsForUser
                        .Where(pr => pr.Id.StartsWith(namespaceToModify.Value, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (packageRegistrationsMatchingNamespace.Count > 0)
                    {
                        packageRegistrationsMatchingNamespace
                            .ForEach(pr => namespaceToModify.PackageRegistrations.Add(pr));

                        await _packageService.UpdatePackageVerifiedStatusAsync(packageRegistrationsMatchingNamespace, isVerified: true);
                    }
                }

                namespaceToModify.Owners.Add(userToAdd);
                await _reservedNamespaceRepository.CommitChangesAsync();

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
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                var namespaceToModify = FindReservedNamespaceForPrefix(prefix.Value);
                if (namespaceToModify == null)
                {
                    throw new InvalidOperationException($"Namespace '{prefix.Value}' not found.");
                }

                var userToRemove = _userService.FindByUsername(user.Username);
                if (userToRemove == null)
                {
                    throw new InvalidOperationException($"User not found with username: {user.Username}");
                }

                if (namespaceToModify.Owners.Contains(userToRemove))
                {
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

                    removeVerifiedMarksForPackages
                        .ForEach(pr => namespaceToModify.PackageRegistrations.Remove(pr));

                    await _packageService.UpdatePackageVerifiedStatusAsync(removeVerifiedMarksForPackages, isVerified: false);

                    namespaceToModify.Owners.Remove(userToRemove);
                    await _reservedNamespaceRepository.CommitChangesAsync();
                }
                else
                {
                    throw new InvalidOperationException($"User {user.Username} is not an owner of this namespace.");
                }

                transaction.Commit();
            }

            EntitiesConfiguration.SuspendExecutionStrategy = false;
        }

        public ReservedNamespace FindReservedNamespaceForPrefix(string prefix)
        {
            return (from request in _reservedNamespaceRepository.GetAll()
                    where request.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                    select request).FirstOrDefault();
        }

        public IList<ReservedNamespace> FindAllReservedNamespacesForPrefix(string prefix, bool getExactMatches)
        {
            Expression<Func<ReservedNamespace, bool>> prefixMatch = 
                dbPrefix => getExactMatches
                    ? dbPrefix.Value.Equals(prefix)
                    : dbPrefix.Value.StartsWith(prefix);

            return _reservedNamespaceRepository
                .GetAll()
                .Where(prefixMatch)
                .ToList();
        }

        public IList<ReservedNamespace> FindReservedNamespacesForPrefixList(IList<string> prefixList)
        {
            return (from dbPrefix in _reservedNamespaceRepository.GetAll()
                    join queryPrefix in prefixList
                    on dbPrefix.Value.ToLower() equals queryPrefix.ToLower()
                    select dbPrefix).ToList();
        }

        public IList<ReservedNamespace> GetReservedNamespacesForId(string id)
        {
            return (from request in _reservedNamespaceRepository.GetAll()
                    where id.StartsWith(request.Value, StringComparison.OrdinalIgnoreCase)
                    select request).ToList();
        }
    }
}