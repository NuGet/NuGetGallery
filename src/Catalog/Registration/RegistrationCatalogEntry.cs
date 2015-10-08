// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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

        public static KeyValuePair<RegistrationEntryKey, RegistrationCatalogEntry> Promote(string resourceUri, IGraph graph, bool unlistShouldDelete = false)
        {
            INode subject = graph.CreateUriNode(new Uri(resourceUri));
            string version = graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode(Schema.Predicates.Version)).First().Object.ToString();

            RegistrationEntryKey registrationEntryKey = new RegistrationEntryKey(RegistrationKey.Promote(resourceUri, graph), version);

            bool deleteByUnlisting = unlistShouldDelete & !IsListed(subject, graph);

            RegistrationCatalogEntry registrationCatalogEntry = deleteByUnlisting | IsDelete(subject, graph) ? null : new RegistrationCatalogEntry(resourceUri, graph);

            return new KeyValuePair<RegistrationEntryKey, RegistrationCatalogEntry>(registrationEntryKey, registrationCatalogEntry);
        }

        static bool IsDelete(INode subject, IGraph graph)
        {
            return graph.ContainsTriple(new Triple(subject, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.CatalogDelete)))
                || graph.ContainsTriple(new Triple(subject, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.PackageDelete)));
        }

        static bool IsListed(INode subject, IGraph graph)
        {
            //return graph.ContainsTriple(new Triple(subject, graph.CreateUriNode(Schema.Predicates.Listed), graph.CreateLiteralNode("true", Schema.DataTypes.Boolean)));

            Triple listed = graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode(Schema.Predicates.Listed)).FirstOrDefault();
            if (listed != null)
            {
                return ((ILiteralNode)listed.Object).Value.Equals("true", StringComparison.InvariantCultureIgnoreCase);
            }
            Triple published = graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode(Schema.Predicates.Published)).FirstOrDefault();
            if (published != null)
            {
                DateTime publishedDate = DateTime.Parse(((ILiteralNode)published.Object).Value);
                return publishedDate.Year != 1900;
            }
            return true;
        }
    }
}
