using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public interface IRegistrationPersistence
    {
        Task<IDictionary<RegistrationEntryKey, RegistrationCatalogEntry>> Load();
        Task Save(IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> registration);
    }
}
