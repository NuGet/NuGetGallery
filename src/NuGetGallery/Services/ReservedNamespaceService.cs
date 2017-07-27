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
        private readonly IEntityRepository<User> _userRepository;
        private readonly IAuditingService _auditingService;

        protected ReservedNamespaceService() { }

        public ReservedNamespaceService(
            IEntityRepository<ReservedNamespace> reservedNamespaceRepository,
            IEntityRepository<User> userRepository,
            IAuditingService auditing)
            : this()
        {
            _reservedNamespaceRepository = reservedNamespaceRepository;
            _userRepository = userRepository;
            _auditingService = auditing;
        }

        public async Task AddReservedNamespaceAsync(ReservedNamespace prefix)
        {
            // Validate if prefix is existing one or new here?
            _reservedNamespaceRepository.InsertOnCommit(prefix);
            await _reservedNamespaceRepository.CommitChangesAsync();
        }

        public Task AddUserToReservedNamespaceAsync(ReservedNamespace prefix, User user)
        {
            throw new NotImplementedException();
        }

        public async Task DeleteReservedNamespaceAsync(ReservedNamespace prefix)
        {
            await Task.Yield();

            // May be get the list and find the one here need comparator for all prefix properties.
            var prefixObject = FindReservedNamespacesForPrefix(prefix.Value);
            _reservedNamespaceRepository.DeleteOnCommit(prefixObject);

            await _reservedNamespaceRepository.CommitChangesAsync();
        }

        public Task DeleteUserFromReservedNamespaceAsync(ReservedNamespace prefix, User user)
        {
            throw new NotImplementedException();
        }

        public ReservedNamespace FindReservedNamespacesForPrefix(string prefix)
        {
            return (from request in _reservedNamespaceRepository.GetAll()
                    where request.Value == prefix
                    select request).FirstOrDefault();
        }

        public IList<ReservedNamespace> GetAllReservedNamespacesForUser(User user)
        {
            throw new NotImplementedException();
        }

        public IList<User> GetAllUsersForNamespace(ReservedNamespace prefix)
        {
            throw new NotImplementedException();
        }
    }
}