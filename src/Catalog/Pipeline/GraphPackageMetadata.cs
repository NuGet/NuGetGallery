// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public class GraphPackageMetadata : PackageMetadataBase
    {
        public GraphPackageMetadata()
            : this(new Graph())
        {
        }

        public GraphPackageMetadata(IGraph graph)
        {
            Graph = graph;
        }

        public IGraph Graph { get; private set; }

        public override void Merge(PackageMetadataBase other)
        {
            IGraph otherGraph = (other is GraphPackageMetadata) ? ((GraphPackageMetadata)other).Graph : Utils.CreateGraph(other.ToContent());
            Graph.Merge(otherGraph, true);
        }

        public override JToken ToContent(JObject frame = null)
        {
            return Utils.CreateJson2(Graph, frame);
        }
    }
}
