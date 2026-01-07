// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// An immutable graph supporting multi-threaded read-only access. According to dotNetRDF documentation, the
    /// <see cref="Graph"/> class is thread-safe when used in a read-only manner.
    /// <see cref="http://www.dotnetrdf.org/api/html/T_VDS_RDF_Graph.htm"/>
    /// </summary>
    public class ReadOnlyGraph : Graph
    {
        private readonly bool _isReadOnly;

        public ReadOnlyGraph(IGraph graph)
        {
            Merge(graph);
            _isReadOnly = true;
        }
        
        public override bool Assert(IEnumerable<Triple> ts)
        {
            ThrowIfReadOnly();
            return base.Assert(ts);
        }

        public override bool Assert(Triple t)
        {
            ThrowIfReadOnly();
            return base.Assert(t);
        }

        public override bool Retract(IEnumerable<Triple> ts)
        {
            ThrowIfReadOnly();
            return base.Retract(ts);
        }

        public override bool Retract(Triple t)
        {
            ThrowIfReadOnly();
            return base.Retract(t);
        }

        public override void Merge(IGraph g)
        {
            ThrowIfReadOnly();
            base.Merge(g);
        }

        public override void Merge(IGraph g, bool keepOriginalGraphUri)
        {
            ThrowIfReadOnly();
            base.Merge(g, keepOriginalGraphUri);
        }

        private void ThrowIfReadOnly()
        {
            if (_isReadOnly)
            {
                throw new NotSupportedException("This RDF graph cannot be modified.");
            }
        }
    }
}
