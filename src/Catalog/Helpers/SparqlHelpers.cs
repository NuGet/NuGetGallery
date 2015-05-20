// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    class SparqlHelpers
    {
        public static IGraph Construct(TripleStore store, string sparql)
        {
            return (IGraph)Execute(store, sparql);
        }

        public static SparqlResultSet Select(TripleStore store, string sparql)
        {
            return (SparqlResultSet)Execute(store, sparql);
        }

        static object Execute(TripleStore store, string sparql)
        {
            InMemoryDataset ds = new InMemoryDataset(store);
            ISparqlQueryProcessor processor = new LeviathanQueryProcessor(ds);
            SparqlQueryParser sparqlparser = new SparqlQueryParser();
            SparqlQuery query = sparqlparser.ParseFromString(sparql);
            return processor.ProcessQuery(query);
        }
    }
}
