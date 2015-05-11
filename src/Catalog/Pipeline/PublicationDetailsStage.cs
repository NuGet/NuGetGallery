// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public class PublicationDetailsStage : PackagePipelineStage
    {
        public override bool Execute(PipelinePackage package, PackagePipelineContext context)
        {
            IGraph graph = new Graph();

            INode subject = graph.CreateUriNode(context.Uri);

            graph.Assert(
                subject,
                graph.CreateUriNode(Schema.Predicates.Published), 
                graph.CreateLiteralNode(package.Published.ToString("O"), Schema.DataTypes.DateTime));

            if (package.Owner != null)
            {
                graph.Assert(
                    subject,
                    graph.CreateUriNode(Schema.Predicates.Owner),
                    graph.CreateLiteralNode(package.Owner));
            }

            context.StageResults.Add(new GraphPackageMetadata(graph));

            return true;
        }
    }
}
