// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    /// <summary>
    /// A delegate used to determine whether a package should be included in a registration hive. The delegate is
    /// important because some registration hives exclude specific packages (typically packages that break older
    /// clients). The first example of this difference is SemVer 2.0.0 packages, which should be excluded from the legacy
    /// registration hives.
    /// </summary>
    /// <param name="key">The package key. This contains the ID and version of the package.</param>
    /// <param name="resourceUri">The URI (identifier) of the package in the RDF graph.</param>
    /// <param name="graph">The RDF graph containing metadata about the package.</param>
    /// <returns>True if the package should be included in the registration hive. False otherwise.</returns>
    public delegate bool ShouldIncludeRegistrationPackage(RegistrationEntryKey key, string resourceUri, IGraph graph);

    public class RegistrationCatalogEntry
    {
        public RegistrationCatalogEntry(string resourceUri, IGraph graph, bool isExistingItem)
        {
            ResourceUri = resourceUri;
            Graph = graph;
            IsExistingItem = isExistingItem;
        }

        public string ResourceUri { get; set; }
        public IGraph Graph { get; set; }
        public bool IsExistingItem { get; set; }

        public static KeyValuePair<RegistrationEntryKey, RegistrationCatalogEntry> Promote(
            string resourceUri,
            IGraph graph,
            ShouldIncludeRegistrationPackage shouldInclude,
            bool isExistingItem)
        {
            INode subject = graph.CreateUriNode(new Uri(resourceUri));
            string version = graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode(Schema.Predicates.Version)).First().Object.ToString();

            RegistrationEntryKey registrationEntryKey = new RegistrationEntryKey(RegistrationKey.Promote(resourceUri, graph), version);

            RegistrationCatalogEntry registrationCatalogEntry = null;
            if (!IsDelete(subject, graph) && shouldInclude(registrationEntryKey, resourceUri, graph))
            {
                registrationCatalogEntry = new RegistrationCatalogEntry(resourceUri, graph, isExistingItem);
            }

            return new KeyValuePair<RegistrationEntryKey, RegistrationCatalogEntry>(registrationEntryKey, registrationCatalogEntry);
        }

        static bool IsDelete(INode subject, IGraph graph)
        {
            return graph.ContainsTriple(new Triple(subject, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.CatalogDelete)))
                || graph.ContainsTriple(new Triple(subject, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.PackageDelete)));
        }
    }
}
