// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace GitHubVulnerabilities2Db.GraphQL
{
    /// <summary>
    /// Wraps a <typeparamref name="TNode"/> with its <see cref="Cursor"/>.
    /// </summary>
    public class Edge<TNode> where TNode : INode
    {
        public string Cursor { get; set; }
        public TNode Node { get; set; }
    }
}
