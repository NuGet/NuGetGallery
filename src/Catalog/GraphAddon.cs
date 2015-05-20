// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// Holds data that can be added to a graph at a later time.
    /// </summary>
    public abstract class GraphAddon
    {
        /// <summary>
        /// Apply assert data in this GraphAddon into the graph.
        /// </summary>
        public abstract void ApplyToGraph(IGraph graph, IUriNode parent);

        /// <summary>
        /// Creates a sub node of the parent uri.
        /// </summary>
        protected static IUriNode GetSubNode(IGraph graph, Uri mainUri, params string[] children)
        {
            string url = String.Format(CultureInfo.InvariantCulture, "{0}#{1}", mainUri.AbsoluteUri, String.Join("-", children)).ToLowerInvariant().TrimEnd('-');
            return graph.CreateUriNode(new Uri(url));
        }

        /// <summary>
        /// Appends the child strings to the Uri of the parent node.
        /// </summary>
        protected static IUriNode GetSubNode(IGraph graph, IUriNode parentNode, params string[] children)
        {
            string url = String.Format(CultureInfo.InvariantCulture, "{0}/{1}", parentNode.Uri.AbsoluteUri, String.Join("-", children)).ToLowerInvariant().TrimEnd('-');
            return graph.CreateUriNode(new Uri(url));
        }
    }
}
