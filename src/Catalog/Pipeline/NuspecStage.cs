// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Linq;
using System.Xml.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public class NuspecStage : PackagePipelineStage
    {
        public override bool Execute(PipelinePackage package, PackagePipelineContext context)
        {
            XDocument nuspec = PackagePipelineHelpers.GetNuspec(package, context);

            IGraph graph = Utils.CreateNuspecGraph(nuspec, context.BaseAddress.AbsoluteUri + "packages/");

            INode rdfTypePredicate = graph.CreateUriNode(Schema.Predicates.Type);

            Triple resource = graph.GetTriplesWithPredicateObject(rdfTypePredicate, graph.CreateUriNode(Schema.DataTypes.PackageDetails)).First();
            graph.Assert(resource.Subject, rdfTypePredicate, graph.CreateUriNode(Schema.DataTypes.Permalink));

            IGraph pageGraph = CreatePageGraph(resource.Subject, graph);

            context.Uri = ((UriNode)resource.Subject).Uri;

            context.StageResults.Add(new GraphPackageMetadata(graph));

            context.StagePageResults.Add(new GraphPackageMetadata(pageGraph));

            return true;
        }

        IGraph CreatePageGraph(INode subject, IGraph graph)
        {
            Graph pageGraph = new Graph();

            Triple idTriple = graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode(Schema.Predicates.Id)).First();
            Triple versionTriple = graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode(Schema.Predicates.Version)).First();

            pageGraph.Assert(idTriple.CopyTriple(pageGraph));
            pageGraph.Assert(versionTriple.CopyTriple(pageGraph));

            return pageGraph;
        }
    }
}
