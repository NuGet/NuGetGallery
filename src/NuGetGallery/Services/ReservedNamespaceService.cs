using NuGetGallery.Auditing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace NuGetGallery
{
    public class ReservedNamespaceService : IReservedNamespaceService
    {
        private readonly IEntityRepository<ReservedNamespace> _reservedNamespaceRepository;
        private readonly IUserService _userService;
        private readonly IPackageService _packageService;
        private readonly IAuditingService _auditingService;

        protected ReservedNamespaceService() { }

        public ReservedNamespaceService(
            IEntityRepository<ReservedNamespace> reservedNamespaceRepository,
            IUserService userService,
            IPackageService packageService,
            IAuditingService auditing)
            : this()
        {
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

            await Task.Yield();
            _reservedNamespaceRepository.InsertOnCommit(prefix);
            await _reservedNamespaceRepository.CommitChangesAsync();
        }

        public async Task DeleteReservedNamespaceAsync(ReservedNamespace prefix)
        {
            await Task.Yield();

            var namespaceToDelete = FindReservedNamespaceForPrefix(prefix.Value);
            // Delete verified tags on corresponding packages for this prefix if this is the only prefix matching the 
            // package registration or the only prefix with no shared namespace.
            _reservedNamespaceRepository.DeleteOnCommit(namespaceToDelete);

            await _reservedNamespaceRepository.CommitChangesAsync();
        }

        public async Task AddOwnerToReservedNamespaceAsync(ReservedNamespace prefix, User user)
        {
            await Task.Yield();

            var namespaceToModify = FindReservedNamespaceForPrefix(prefix.Value);
            var userToAdd = _userService.FindByUsername(user.Username);
            if (userToAdd != null)
            {
                var allPackageRegistrationsForUser = _packageService.FindPackageRegistrationsByOwner(userToAdd);
                var packageRegistrationsMatchingPrefix = allPackageRegistrationsForUser
                    .Where(pr =>
                        (namespaceToModify.IsPrefix && pr.Id.StartsWith(namespaceToModify.Value, StringComparison.OrdinalIgnoreCase))
                        || (!namespaceToModify.IsPrefix && pr.Id.Equals(namespaceToModify.Value, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (packageRegistrationsMatchingPrefix.Count > 0)
                {
                    // Can duplicate package registrations be added in here?
                    packageRegistrationsMatchingPrefix
                        .ForEach(pr => namespaceToModify.PackageRegistrations.Add(pr));

                    // Might need a batch transaction in here?
                    packageRegistrationsMatchingPrefix
                        .ForEach(pr => _packageService.UpdatePackageVerifiedStatusAsync(pr, isVerified: !namespaceToModify.IsSharedNamespace));
                }

                namespaceToModify.Owners.Add(userToAdd);
                await _reservedNamespaceRepository.CommitChangesAsync();
            }
            else
            {
                throw new Exception("User not found with username: " + user.Username);
            }
        }

        public async Task DeleteOwnerFromReservedNamespaceAsync(ReservedNamespace prefix, User user)
        {
            await Task.Yield();

            var namespaceToModify = FindReservedNamespaceForPrefix(prefix.Value);
            var userToRemove = _userService.FindByUsername(user.Username);
            if (userToRemove != null)
            {
                if (namespaceToModify.Owners.Contains(userToRemove))
                {
                    var packagesOwnedByUserMatchingPrefix = namespaceToModify.PackageRegistrations
                        .Where(pr => 
                            pr.Owners.Any(pro => pro.Username == userToRemove.Username))
                        .ToList();

                    // Remove verified mark for package registrations if the user to be removed is the only prefix owner
                    // for the given package registration.
                    var removeVerifiedMarksForPackages = packagesOwnedByUserMatchingPrefix
                        .Where(pr => pr.Owners.Intersect(namespaceToModify.Owners).Count() == 1)
                        .ToList();

                    removeVerifiedMarksForPackages
                        .ForEach(pr => namespaceToModify.PackageRegistrations.Remove(pr));

                    // Need a transaction here?
                    removeVerifiedMarksForPackages
                        .ForEach(pr => _packageService.UpdatePackageVerifiedStatusAsync(pr, isVerified: false));

                    namespaceToModify.Owners.Remove(userToRemove);
                    await _reservedNamespaceRepository.CommitChangesAsync();
                }
                else
                {
                    throw new Exception($"User {user.Username} is not an owner of this namespace.");
                }
            }
            else
            {
                throw new Exception($"User not found with username: {user.Username}");
            }
        }

        public ReservedNamespace FindReservedNamespaceForPrefix(string prefix)
        {
            return (from request in _reservedNamespaceRepository.GetAll()
                    where request.Value == prefix
                    select request).FirstOrDefault();
        }

        public IList<ReservedNamespace> FindAllReservedNamespacesForPrefix(string prefix)
        {
            return (from request in _reservedNamespaceRepository.GetAll()
                    where request.Value.StartsWith(prefix)
                    select request).ToList();
        }

        public IList<ReservedNamespace> GetAllReservedNamespacesForUser(User user)
        {
            var userObject = _userService.FindByUsername(user.Username);
            return userObject.ReservedNamespaces.ToList();
        }

        public IList<User> GetAllOwnersForNamespace(ReservedNamespace prefix)
        {
            var prefixObject = FindReservedNamespaceForPrefix(prefix.Value);
            return prefixObject.Owners.ToList();
        }
    }
}