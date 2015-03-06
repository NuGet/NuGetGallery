using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationCatalogEntry
    {
        public RegistrationCatalogEntry(string resourceUri, IGraph graph)
        {
            ResourceUri = resourceUri;
            Graph = graph;
        }
        public string ResourceUri { get; set; }
        public IGraph Graph { get; set; }

        public static KeyValuePair<RegistrationEntryKey, RegistrationCatalogEntry> Promote(string resourceUri, IGraph graph)
        {
            INode subject = graph.CreateUriNode(new Uri(resourceUri));
            string version = graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode(Schema.Predicates.Version)).First().Object.ToString();

            RegistrationEntryKey registrationEntryKey = new RegistrationEntryKey(RegistrationKey.Promote(resourceUri, graph), version);

            RegistrationCatalogEntry registrationCatalogEntry = IsDelete(subject, graph) ? null : new RegistrationCatalogEntry(resourceUri, graph);

            return new KeyValuePair<RegistrationEntryKey, RegistrationCatalogEntry>(registrationEntryKey, registrationCatalogEntry);
        }

        static bool IsDelete(INode subject, IGraph graph)
        {
            return graph.ContainsTriple(new Triple(subject, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.CatalogDelete)));
        }
    }
}
