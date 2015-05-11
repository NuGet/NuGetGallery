// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Canton
{
    /// <summary>
    /// Adds canton:origin to the graph
    /// </summary>
    public class OriginGraphAddon : GraphAddon
    {
        private readonly string _origin;
        private readonly int _commitId;

        public OriginGraphAddon(string origin, int cantonCommitId)
            : base()
        {
            _origin = origin;
            _commitId = cantonCommitId;
        }

        public override void ApplyToGraph(IGraph graph, IUriNode parent)
        {
            graph.Assert(parent, graph.CreateUriNode(new Uri(CantonConstants.CantonSchema + "origin")), graph.CreateLiteralNode(_origin));
            graph.Assert(parent, graph.CreateUriNode(new Uri(CantonConstants.CantonSchema + "commitId")), graph.CreateLiteralNode("" + _commitId));
        }
    }
}
