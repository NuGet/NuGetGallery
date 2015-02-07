using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Publish
{
    public class StorageRegistration : IRegistration
    {
        Storage _storage;

        public StorageRegistration(StorageFactory storageFactory)
        {
            _storage = storageFactory.Create();
        }

        public async Task AddOwner(RegistrationId registrationId, string owner)
        {
            Uri resourceUri = new Uri(_storage.BaseAddress, "index.json");

            IGraph graph = Utils.CreateGraph(await _storage.LoadString(resourceUri)) ?? new Graph();

            INode record = graph.CreateUriNode(resourceUri);
            INode domain = graph.CreateUriNode(new Uri(resourceUri.AbsoluteUri + "#" + registrationId.Domain));
            INode registration = graph.CreateUriNode(new Uri(resourceUri.AbsoluteUri + "#" + registrationId.RelativeAddress));

            graph.Assert(record, graph.CreateUriNode(Schema.Predicates.RecordDomain), domain);
            graph.Assert(record, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.Record));
            graph.Assert(domain, graph.CreateUriNode(Schema.Predicates.RecordRegistration), registration);
            graph.Assert(domain, graph.CreateUriNode(Schema.Predicates.Domain), graph.CreateLiteralNode(registrationId.Domain));
            graph.Assert(registration, graph.CreateUriNode(Schema.Predicates.Id), graph.CreateLiteralNode(registrationId.Id));
            graph.Assert(registration, graph.CreateUriNode(Schema.Predicates.Owner), graph.CreateLiteralNode(owner));

            StorageContent content = CreateContent(graph, Schema.DataTypes.Record);

            await _storage.Save(resourceUri, content);
        }

        public Task RemoveOwner(RegistrationId registrationId, string owner)
        {
            throw new System.NotImplementedException();
        }

        public Task Add(PackageId packageId)
        {
            throw new System.NotImplementedException();
        }

        public Task Remove(PackageId packageId)
        {
            throw new System.NotImplementedException();
        }

        public Task Remove(RegistrationId registrationId)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> Exists(RegistrationId registrationId)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> Exists(PackageId packageId)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> HasOwner(RegistrationId registrationId, string owner)
        {
            throw new System.NotImplementedException();
        }

        static StorageContent CreateContent(IGraph graph, Uri type)
        {
            //TODO: put context in resources
            //JObject frame = Context.GetJsonLdContext("context.Container.json", type);

            JObject frame = new JObject
            {
                { "@context", new JObject { { "@vocab", "http://schema.nuget.org/record#" } } },
                { "@type", type.AbsoluteUri }
            };

            return new StringStorageContent(Utils.CreateJson(graph, frame), "application/json", "no-store");
        }
    }
}
