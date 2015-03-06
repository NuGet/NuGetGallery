using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public static class RegistrationMaker
    {
        public static async Task Process(RegistrationKey registrationKey, IDictionary<string, IGraph> newItems, StorageFactory storageFactory, Uri contentBaseAddress, int partitionSize, int packageCountThreshold)
        {
            IRegistrationPersistence registration = new RegistrationPersistence(storageFactory, registrationKey, partitionSize, packageCountThreshold);
            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> existing = await registration.Load();
            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> delta = PromoteRegistrationKey(newItems);
            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> resulting = Apply(existing, delta);
            await registration.Save(resulting);
        }

        static IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> PromoteRegistrationKey(IDictionary<string, IGraph> newItems)
        {
            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> promoted = new Dictionary<RegistrationEntryKey, RegistrationCatalogEntry>();
            foreach (var newItem in newItems)
            {
                promoted.Add(RegistrationCatalogEntry.Promote(newItem.Key, newItem.Value));
            }
            return promoted;
        }

        static IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> Apply(
            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> existing, 
            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> delta)
        {
            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> resulting = new Dictionary<RegistrationEntryKey, RegistrationCatalogEntry>();

            foreach (var item in existing)
            {
                if (delta.ContainsKey(item.Key))
                {
                    resulting.Add(item.Key, delta[item.Key]);
                    delta.Remove(item.Key);
                }
                else
                {
                    resulting.Add(item);
                }
            }

            foreach (var item in delta)
            {
                resulting.Add(item);
            }

            return resulting;
        }
    }
}
