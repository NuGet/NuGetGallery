
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public interface IRegistration
    {
        Task EnableTenant(string tenant);
        Task DisableTenant(string tenant);
        Task<bool> IsTenantEnabled(string tenant);

        Task AddOwner(RegistrationId registrationId, string owner);
        Task RemoveOwner(RegistrationId registrationId, string owner);

        Task Add(PackageId packageId);
        Task Remove(PackageId packageId);
        Task Remove(RegistrationId registrationId);

        Task<bool> Exists(RegistrationId registrationId);
        Task<bool> Exists(PackageId packageId);
        Task<bool> HasOwner(RegistrationId registrationId, string owner);

        Task<IEnumerable<string>> GetOwners(RegistrationId registrationId);
        Task<IEnumerable<RegistrationId>> GetRegistrations(string owner);
        Task<IEnumerable<PackageId>> GetPackages(RegistrationId registrationId);
    }
}
