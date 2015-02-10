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

        public async Task AddOwner(RegistrationId registrationId, string owner)
        {
            Uri resourceUri = new Uri(_storage.BaseAddress, "index.json");

            IGraph graph = Utils.CreateGraph(await _storage.LoadString(resourceUri)) ?? new Graph();

            //  (re)assert the registration

            INode registration = AssertRegistration(graph, resourceUri, registrationId);

            //  add the owner to the registration

            graph.Assert(registration, graph.CreateUriNode(Schema.Predicates.Owner), graph.CreateLiteralNode(owner));

            await Save(resourceUri, graph);
        }

        public async Task RemoveOwner(RegistrationId registrationId, string owner)
        {
            Uri resourceUri = new Uri(_storage.BaseAddress, "index.json");

            IGraph graph = Utils.CreateGraph(await _storage.LoadString(resourceUri));

            if (graph == null)
            {
                return;
            }

            INode record = graph.CreateUriNode(resourceUri);
            INode domain = graph.CreateUriNode(new Uri(resourceUri.AbsoluteUri + "#" + registrationId.Domain));
            INode registration = graph.CreateUriNode(new Uri(resourceUri.AbsoluteUri + "#" + registrationId.RegistrationRelativeAddress));

            //  remove the owner from this registration

            graph.Retract(registration, graph.CreateUriNode(Schema.Predicates.Owner), graph.CreateLiteralNode(owner));

            //  if there are no more owners remove all mention of this registration

            if (graph.GetTriplesWithSubjectPredicate(registration, graph.CreateUriNode(Schema.Predicates.Owner)).Count() == 0)
            {
                RetractAll(graph, registration);
            }

            //  if there are no more registrations in this domain remove the domain 

            if (graph.GetTriplesWithSubjectPredicate(domain, graph.CreateUriNode(Schema.Predicates.RecordRegistration)).Count() == 0)
            {
                RetractAll(graph, domain);
            }

            await Save(resourceUri, graph);
        }

        public async Task Add(PackageId packageId)
        {
            Uri resourceUri = new Uri(_storage.BaseAddress, "index.json");

            IGraph graph = Utils.CreateGraph(await _storage.LoadString(resourceUri)) ?? new Graph();

            //  (re)assert the registration

            INode registration = AssertRegistration(graph, resourceUri, packageId);

            //  add the version to the registration

            graph.Assert(registration, graph.CreateUriNode(Schema.Predicates.Version), graph.CreateLiteralNode(packageId.Version.ToNormalizedString()));

            await Save(resourceUri, graph);
        }

        public async Task Remove(PackageId packageId)
        {
            Uri resourceUri = new Uri(_storage.BaseAddress, "index.json");

            IGraph graph = Utils.CreateGraph(await _storage.LoadString(resourceUri));

            if (graph == null)
            {
                return;
            }

            INode record = graph.CreateUriNode(resourceUri);
            INode domain = graph.CreateUriNode(new Uri(resourceUri.AbsoluteUri + "#" + packageId.Domain));
            INode registration = graph.CreateUriNode(new Uri(resourceUri.AbsoluteUri + "#" + packageId.RegistrationRelativeAddress));

            //  remove the version from this registration

            graph.Retract(registration, graph.CreateUriNode(Schema.Predicates.Version), graph.CreateLiteralNode(packageId.Version.ToNormalizedString()));

            //  even removing all the versions of a package leaves the registraton behind

            await Save(resourceUri, graph);
        }

        public async Task Remove(RegistrationId registrationId)
        {
            Uri resourceUri = new Uri(_storage.BaseAddress, "index.json");

            IGraph graph = Utils.CreateGraph(await _storage.LoadString(resourceUri));

            if (graph == null)
            {
                return;
            }

            INode record = graph.CreateUriNode(resourceUri);
            INode domain = graph.CreateUriNode(new Uri(resourceUri.AbsoluteUri + "#" + registrationId.Domain));
            INode registration = graph.CreateUriNode(new Uri(resourceUri.AbsoluteUri + "#" + registrationId.RegistrationRelativeAddress));

            //  remove the registration

            RetractAll(graph, registration);

            //  if there are no more registrations in this domain remove the domain 

            if (graph.GetTriplesWithSubjectPredicate(domain, graph.CreateUriNode(Schema.Predicates.RecordRegistration)).Count() == 0)
            {
                RetractAll(graph, domain);
            }

            await Save(resourceUri, graph);
        }

        public async Task<bool> Exists(RegistrationId registrationId)
        {
            Uri resourceUri = new Uri(_storage.BaseAddress, "index.json");
            IGraph graph = Utils.CreateGraph(await _storage.LoadString(resourceUri));

            if (graph != null)
            {
                INode registration = graph.CreateUriNode(new Uri(resourceUri.AbsoluteUri + "#" + registrationId.RegistrationRelativeAddress));
                if (graph.GetTriplesWithSubject(registration).Count() > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> Exists(PackageId packageId)
        {
            Uri resourceUri = new Uri(_storage.BaseAddress, "index.json");
            IGraph graph = Utils.CreateGraph(await _storage.LoadString(resourceUri));

            if (graph != null)
            {
                INode registration = graph.CreateUriNode(new Uri(resourceUri.AbsoluteUri + "#" + packageId.RegistrationRelativeAddress));
                return graph.ContainsTriple(new Triple(registration, graph.CreateUriNode(Schema.Predicates.Version), graph.CreateLiteralNode(packageId.Version.ToNormalizedString())));
            }

            return false;
        }

        public async Task<bool> HasOwner(RegistrationId registrationId, string owner)
        {
            Uri resourceUri = new Uri(_storage.BaseAddress, "index.json");
            IGraph graph = Utils.CreateGraph(await _storage.LoadString(resourceUri));

            if (graph != null)
            {
                INode registration = graph.CreateUriNode(new Uri(resourceUri.AbsoluteUri + "#" + registrationId.RegistrationRelativeAddress));
                return graph.ContainsTriple(new Triple(registration, graph.CreateUriNode(Schema.Predicates.Owner), graph.CreateLiteralNode(owner)));
            }

            return false;
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

        static void RetractAll(IGraph graph, INode node)
        {
            IList<Triple> triples = new List<Triple>(graph.GetTriples(node));
            foreach (Triple triple in triples)
            {
                graph.Retract(triple);
            }
        }

        static INode AssertRegistration(IGraph graph, Uri resourceUri, RegistrationId registrationId)
        {
            INode record = graph.CreateUriNode(resourceUri);
            INode domain = graph.CreateUriNode(new Uri(resourceUri.AbsoluteUri + "#" + registrationId.Domain));
            INode registration = graph.CreateUriNode(new Uri(resourceUri.AbsoluteUri + "#" + registrationId.RegistrationRelativeAddress));

            //  add the root Record

            graph.Assert(record, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.Record));

            //  add the domain to the record

            graph.Assert(record, graph.CreateUriNode(Schema.Predicates.RecordDomain), domain);

            //  add the registration to the domain

            graph.Assert(domain, graph.CreateUriNode(Schema.Predicates.RecordRegistration), registration);
            graph.Assert(domain, graph.CreateUriNode(Schema.Predicates.Domain), graph.CreateLiteralNode(registrationId.Domain));
            graph.Assert(registration, graph.CreateUriNode(Schema.Predicates.Id), graph.CreateLiteralNode(registrationId.Id));

            return registration;
        }
    }
}
