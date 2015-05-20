// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public class CommitDetailsStage : PackagePipelineStage
    {
        public override bool Execute(PipelinePackage package, PackagePipelineContext context)
        {
            DateTime? commitTimeStamp = PackagePipelineHelpers.GetCommitTimeStamp(context);
            Guid? commitId = PackagePipelineHelpers.GetCommitId(context);

            IGraph graph = new Graph();

            INode resource = graph.CreateUriNode(context.Uri);

            if (commitTimeStamp != null)
            {
                graph.Assert(
                    resource,
                    graph.CreateUriNode(Schema.Predicates.CatalogTimeStamp),
                    graph.CreateLiteralNode(commitTimeStamp.Value.ToString("O"), Schema.DataTypes.DateTime));
            }

            if (commitId != null)
            {
                graph.Assert(
                    resource,
                    graph.CreateUriNode(Schema.Predicates.CatalogCommitId),
                    graph.CreateLiteralNode(commitId.Value.ToString()));
            }

            context.StageResults.Add(new GraphPackageMetadata(graph));

            return true;
        }
    }
}
