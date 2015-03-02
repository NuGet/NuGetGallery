
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public interface IRegistration
    {
        Task EnableTenant(string tenant);
        Task DisableTenant(string tenant);
        Task<bool> HasTenantEnabled(string tenant);

        Task AddOwner(OwnershipRegistration registration, OwnershipOwner owner);
        Task RemoveOwner(OwnershipRegistration registration, OwnershipOwner owner);
        
        Task AddVersion(OwnershipRegistration registration, OwnershipOwner owner, string version);
        Task RemoveVersion(OwnershipRegistration registration, string version);
        
        Task Remove(OwnershipRegistration registration);
        
        Task<bool> HasRegistration(OwnershipRegistration registration);
        Task<bool> HasVersion(OwnershipRegistration registration, string version);
        Task<bool> HasOwner(OwnershipRegistration registration, OwnershipOwner owner);
        
        Task<IEnumerable<OwnershipOwner>> GetOwners(OwnershipRegistration registration);
        Task<IEnumerable<OwnershipRegistration>> GetRegistrations(OwnershipOwner owner);
        Task<IEnumerable<string>> GetVersions(OwnershipRegistration registration);
    }
}
