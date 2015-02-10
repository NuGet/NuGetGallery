
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public interface IRegistration
    {
        Task AddTenant(string tenant);
        Task RemoveTenant(string tenant);
        Task<bool> HasTenant(string tenant);

        Task AddOwner(RegistrationId registrationId, string owner);
        Task RemoveOwner(RegistrationId registrationId, string owner);

        Task Add(PackageId packageId);
        Task Remove(PackageId packageId);
        Task Remove(RegistrationId registrationId);

        Task<bool> Exists(RegistrationId registrationId);
        Task<bool> Exists(PackageId packageId);
        Task<bool> HasOwner(RegistrationId registrationId, string owner);
    }
}
