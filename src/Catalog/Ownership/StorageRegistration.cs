using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public class StorageRegistration : IRegistration
    {
        Storage _storage;

        public StorageRegistration(StorageFactory storageFactory)
        {
            _storage = storageFactory.Create();
        }

        public async Task EnableTenant(string tenant)
        {
            OwnershipRecord record = await Load();
            record.EnableTenant(tenant);
            await Save(record);
        }

        public async Task DisableTenant(string tenant)
        {
            OwnershipRecord record = await Load();
            record.DisableTenant(tenant);
            await Save(record);
        }

        public async Task<bool> HasTenantEnabled(string tenant)
        {
            OwnershipRecord record = await Load();
            return record.HasTenantEnabled(tenant);
        }

        public async Task AddOwner(OwnershipRegistration registration, OwnershipOwner owner)
        {
            OwnershipRecord record = await Load();
            record.AddOwner(registration, owner);
            await Save(record);
        }

        public async Task RemoveOwner(OwnershipRegistration registration, OwnershipOwner owner)
        {
            OwnershipRecord record = await Load();
            record.RemoveOwnerFromRegistration(registration, owner);
            await Save(record);
        }

        public async Task AddVersion(OwnershipRegistration registration, OwnershipOwner owner, string version)
        {
            OwnershipRecord record = await Load();
            record.AddVersion(registration, owner, version);
            await Save(record);
        }

        public async Task RemoveVersion(OwnershipRegistration registration, string version)
        {
            OwnershipRecord record = await Load();
            record.RemoveVersion(registration, version);
            await Save(record);
        }

        public async Task Remove(OwnershipRegistration registration)
        {
            OwnershipRecord record = await Load();
            record.RemoveRegistration(registration);
            await Save(record);
        }

        public async Task<bool> HasRegistration(OwnershipRegistration registration)
        {
            OwnershipRecord record = await Load();
            return record.HasRegistration(registration);
        }

        public async Task<bool> HasVersion(OwnershipRegistration registration, string version)
        {
            OwnershipRecord record = await Load();
            return record.HasVersion(registration, version);
        }

        public async Task<bool> HasOwner(OwnershipRegistration registration, OwnershipOwner owner)
        {
            OwnershipRecord record = await Load();
            return record.HasOwner(registration, owner);
        }

        public async Task<IEnumerable<OwnershipOwner>> GetOwners(OwnershipRegistration registration)
        {
            OwnershipRecord record = await Load();
            return record.GetOwners(registration);
        }

        public async Task<IEnumerable<OwnershipRegistration>> GetRegistrations(OwnershipOwner owner)
        {
            OwnershipRecord record = await Load();
            return record.GetRegistrations(owner);
        }

        public async Task<IEnumerable<string>> GetVersions(OwnershipRegistration registration)
        {
            OwnershipRecord record = await Load();
            return record.GetVersions(registration);
        }

        //  implementation helpers

        async Task<OwnershipRecord> Load()
        {
            Uri resourceUri = new Uri(_storage.BaseAddress, "index.json");
            IGraph graph = Utils.CreateGraph(await _storage.LoadString(resourceUri));
            return new OwnershipRecord(resourceUri, graph);
        }

        async Task Save(OwnershipRecord record)
        {
            await Save(record.Base, record.Graph);
        }

        static StorageContent CreateContent(IGraph graph, Uri type)
        {
            JObject frame = (new CatalogContext()).GetJsonLdContext("context.Ownership.json", type);
            return new StringStorageContent(Utils.CreateJson(graph, frame), "application/json", "no-store");
        }

        async Task Save(Uri resourceUri, IGraph graph)
        {
            SetTimestamp(resourceUri, graph);
            StorageContent content = CreateContent(graph, Schema.DataTypes.Record);
            await _storage.Save(resourceUri, content);
        }

        static void SetTimestamp(Uri resourceUri, IGraph graph)
        {
            INode subject = graph.CreateUriNode(resourceUri);

            Triple triple = graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode(Schema.Predicates.CatalogTimeStamp)).FirstOrDefault();
            if (triple != null)
            {
                graph.Retract(triple);
            }

            DateTime timestamp = DateTime.UtcNow;

            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.CatalogTimeStamp), graph.CreateLiteralNode(timestamp.ToString("O")));
        }
    }
}
