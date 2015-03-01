using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Writing;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public static class RegistrationMaker
    {
        public static async Task Process(string id, IDictionary<string, IGraph> newItems, StorageFactory storageFactory, Uri contentBaseAddress, int partitionSize, int packageCountThreshold)
        {
            RegistrationPersistence registration = new RegistrationPersistence(storageFactory, id, partitionSize, packageCountThreshold);

            IDictionary<RegistrationKey, Tuple<string, IGraph>> existing = await registration.Load();

            IDictionary<RegistrationKey, Tuple<string, IGraph>> delta = PromoteKey(newItems);

            IDictionary<RegistrationKey, Tuple<string, IGraph>> resulting = Apply(existing, delta);

            await registration.Save(resulting);
        }

        static IDictionary<RegistrationKey, Tuple<string, IGraph>> PromoteKey(IDictionary<string, IGraph> newItems)
        {
            IDictionary<RegistrationKey, Tuple<string, IGraph>> promoted = new Dictionary<RegistrationKey, Tuple<string, IGraph>>();

            foreach (var newItem in newItems)
            {
                INode subject = newItem.Value.CreateUriNode(new Uri(newItem.Key));

                string id = newItem.Value.GetTriplesWithSubjectPredicate(subject, newItem.Value.CreateUriNode(Schema.Predicates.Id)).First().Object.ToString();
                string version = newItem.Value.GetTriplesWithSubjectPredicate(subject, newItem.Value.CreateUriNode(Schema.Predicates.Version)).First().Object.ToString();

                RegistrationKey resourceKey = new RegistrationKey { Id = id, Version = version };

                promoted.Add(resourceKey, Tuple.Create(newItem.Key, newItem.Value));
            }

            return promoted;
        }

        static IDictionary<RegistrationKey, Tuple<string, IGraph>> Apply(
            IDictionary<RegistrationKey, Tuple<string, IGraph>> existing,
            IDictionary<RegistrationKey, Tuple<string, IGraph>> delta)
        {
            IDictionary<RegistrationKey, Tuple<string, IGraph>> resulting = new Dictionary<RegistrationKey, Tuple<string, IGraph>>();

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
